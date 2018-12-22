module HCup.App

open System
open System.Buffers
open System.IO
open System.IO.Compression
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Text
open System.Linq
open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Logging
open HCup.Models
open HCup.RequestCounter
open HCup.Actors
open HCup.Parser
open HCup.Helpers
open HCup.BufferSerializers
open HCup.MethodCounter
open HCup.Dictionaries
open Giraffe
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.AspNetCore.Server.Kestrel.Transport
open Utf8Json
open System
open Filters
open Microsoft.Extensions.DependencyInjection
open System.Text.RegularExpressions

// ---------------------------------
// Web app
// ---------------------------------

let accounts = Array.zeroCreate 10300


let jsonStringValues = StringValues "application/json"

let inline deserializeObjectUnsafe<'a> (str: string) =
    JsonSerializer.Deserialize<'a>(str)

let inline deserializeObject<'a> (str: string) =
    try
        Some <| JsonSerializer.Deserialize<'a>(str)
    with
    | exn ->
        None

let getStringWeight (str: string) =
    let strChars = str |> Seq.truncate 11
    let mutable multiplier = 50542106513726817L //33^11
    let mutable result = 0L
    for chr in strChars do
        let intChr = int chr
        let diff =
            match intChr with
            | smallRussianLetter when smallRussianLetter >= 1072 -> intChr - 949
            | bigRussianLetter when bigRussianLetter >= 1040 -> intChr - 949
            | smallEnglishLetter when smallEnglishLetter >= 97 -> intChr - 32
            | bigEnglishLetter when bigEnglishLetter >= 65 -> intChr - 32
            | _ -> intChr - 32
        result <- result + (int64 diff) * multiplier
        multiplier <- multiplier / 33L
    result

let inline convertInterestToIndex (interests: string[]) =
    if interests |> isNull
    then null
    else
        interests
        |> Array.map(fun interest ->
                let mutable interestIndex = 0L
                if interestsDictionary.TryGetValue(interest, &interestIndex)
                then
                    interestIndex
                else
                    interestIndex <- getStringWeight interest
                    interestsDictionary.Add(interest, interestIndex)
                    interestIndex
            )

let getAccount (accUpd: AccountUpd): Account =
    let atIndex = accUpd.email.IndexOf('@', StringComparison.Ordinal)
    let emailDomain = accUpd.email.Substring(atIndex+1)
    let phoneCode =
        if accUpd.phone |> isNull
        then 0
        else Int32.Parse(accUpd.phone.Substring(2,3))
    let account = Account()
    account.id <- accUpd.id
    if accUpd.fname |> isNotNull
    then
        let mutable nameIndex = 0L
        if namesDictionary.TryGetValue(accUpd.fname, &nameIndex)
        then
            account.fname <- nameIndex
        else
            nameIndex <- getStringWeight accUpd.fname
            namesDictionary.Add(accUpd.fname, nameIndex)
            account.fname <- nameIndex
    account.sname <- accUpd.sname
    account.email <- accUpd.email
    account.emailDomain <- emailDomain
    account.interests <- convertInterestToIndex accUpd.interests
    account.status <- getStatus accUpd.status
    account.premium <- accUpd.premium
    account.premiumNow <- box accUpd.premium |> isNotNull && accUpd.premium.start <= currentTs && accUpd.premium.finish > currentTs
    account.sex <- accUpd.sex
    account.phone <- accUpd.phone
    account.phoneCode <- phoneCode
    account.likes <- accUpd.likes
    account.birth <- accUpd.birth
    account.birthYear <- (convertToDate accUpd.birth).Year
    account.joined <- accUpd.joined
    account.joinedYear <- (convertToDate accUpd.joined).Year
    if accUpd.city |> isNotNull
    then
        let mutable cityIndex = 0L
        if citiesDictionary.TryGetValue(accUpd.city, &cityIndex)
        then
            account.city <- cityIndex
        else
            cityIndex <- getStringWeight accUpd.city
            citiesDictionary.Add(accUpd.city, cityIndex)
            account.city <- cityIndex
    if accUpd.country |> isNotNull
    then
        let mutable countryIndex = 0L
        if countriesDictionary.TryGetValue(accUpd.country, &countryIndex)
        then
            account.country <- countryIndex
        else
            countryIndex <-  getStringWeight accUpd.country
            countriesDictionary.Add(accUpd.country, countryIndex)
            account.country <- countryIndex
    account


let inline writeResponse (response : MemoryStream) (next : HttpFunc) (ctx: HttpContext) =
    let length = response.Position
    ctx.Response.Headers.["Content-Type"] <- jsonStringValues
    ctx.Response.Headers.ContentLength <- Nullable(length)
    let bytes = response.GetBuffer()
    task {
        do! ctx.Response.Body.WriteAsync(bytes, 0, (int32)length)
        do! ctx.Response.Body.FlushAsync()
        ArrayPool.Shared.Return bytes
        return! next ctx
    }

let getFilteredAccounts (next, ctx : HttpContext) =
    Interlocked.Increment(accountFilterCount) |> ignore
    try
        let keys =
            ctx.Request.Query.Keys
            |> Seq.toArray
            |> Array.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
        let filters =
            keys
            |> Array.map (fun key -> filters.[key] ctx.Request.Query.[key].[0])
        let accounts = accounts |> Array.filter(fun acc -> (box acc) |> isNotNull)
        let accs =
            filters
            |> Array.fold (fun acc f -> acc |> Array.filter f) accounts
            |> Array.sortByDescending (fun acc -> acc.id)
            |> Array.truncate (Int32.Parse(ctx.Request.Query.["limit"].[0]))
        let memoryStream = serializeAccounts (accs, keys)
        writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException ->
        Console.WriteLine("NotSupportedException: " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

let array_sort value =
    if value = -1
    then Array.sortByDescending
    else Array.sortBy

let applyGrouping (memoryStream: byref<MemoryStream>, groupKey, order, accs: Account[], limit) =
    match groupKey with
    | "sex" ->
        let groups =
            accs
            |> Array.groupBy (fun acc -> acc.sex)
            |> Array.map (fun (key, group) -> (key, group.Length))
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroupsSex(groups, "sex")
    | "status" ->
        let groups =
            accs
            |> Array.groupBy (fun acc -> acc.status)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroupsStatus(groups, "status")
    | "country" ->
        let groups =
            accs
            |> Array.groupBy (fun acc -> acc.country)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroupsCountry(groups, "country")
    | "city" ->
        let groups =
            accs
            |> Array.groupBy (fun acc -> acc.city)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroupsCity(groups, "city")
    | "interests" ->
        let interests =
            accs
            |> Array.filter (fun acc -> acc.interests |> isNotNull)
            |> Array.collect (fun acc -> acc.interests)
            |> Array.groupBy id
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroupsInterests(interests , "interests")
    | "city,status" ->
        let groups =
            accs
            |> Array.groupBy (fun acc -> acc.city, acc.status)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups2Status(groups, "city", "status")
    | "city,sex" ->
        let groups =
            accs
            |> Array.groupBy (fun acc -> acc.city, acc.sex)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "city", "sex")
    | "country,sex" ->
        let groups =
            accs
            |> Array.groupBy (fun acc -> acc.country, acc.sex)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "country", "sex")
    | "country,status" ->
        let groups =
            accs
            |> Array.groupBy (fun acc -> acc.country, acc.status)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups2Status(groups, "country", "status")
    | _ ->
        ()


let getGroupedAccounts (next, ctx : HttpContext) =
    Interlocked.Increment(accountsGroupCount) |> ignore
    try
        let keys =
            ctx.Request.Query.Keys
            |> Seq.toArray
            |> Array.filter(fun key -> (key =~ "limit" || key =~ "query_id" || key =~ "order" || key=~"keys") |> not )
        let filters =
            keys
            |> Array.map (fun key -> groupFilters.[key] ctx.Request.Query.[key].[0])
        let groupKey =
            ctx.Request.Query.["keys"].[0]
        let order =
            Int32.Parse(ctx.Request.Query.["order"].[0])
        let accounts = accounts |> Array.filter(fun acc -> (box acc) |> isNotNull)
        let accs =
            filters
            |> Array.fold (fun acc f -> acc |> Array.filter f) accounts
        let mutable memoryStream: MemoryStream = null
        let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
        applyGrouping(&memoryStream, groupKey, order, accs, limit)
        if (memoryStream |> isNotNull)
        then writeResponse memoryStream next ctx
        else setStatusCode 400 next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

let sortAccount (acc: Account) =
    let hasPremium =
        box acc.premium |> isNotNull && acc.premium.start <= currentTs && acc.premium.finish > currentTs
    hasPremium

let getRecommendedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsRecommendCount) |> ignore
    if (id > accounts.Length)
    then
        setStatusCode 404 next ctx
    else
        let target = accounts.[id]
        let accounts = accounts |> Array.filter(fun acc -> (box acc) |> isNotNull && acc.interests |> isNotNull)
        //let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
        //let accs =
        //    accounts
        //    |> Array.sortBy sortAccount
        //let memoryStream = serializeAccounts (accs, keys)
        //writeResponse memoryStream next ctx
        // setStatusCode 400 next ctx



        json 1 next ctx

let getSuggestedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsSuggestCount) |> ignore
    setStatusCode 401 next ctx

let private accountsFilterString = "/accounts/filter/"
let private accountsGroupString = "/accounts/group/"

let customGetRoutef : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.Request.Path.Value with
        | filterPath when filterPath =~ accountsFilterString ->
             getFilteredAccounts (next, ctx)
        | filterPath when filterPath =~ accountsGroupString ->
             getGroupedAccounts (next, ctx)
        | filterPath ->
            if filterPath.Length > 10
            then
                let sp = filterPath.AsSpan()
                let mainPart = sp.Slice(10)
                let indexOfSlash = mainPart.IndexOf('/')
                if indexOfSlash > 0
                then
                    let stringId = mainPart.Slice(0,indexOfSlash)
                    let ending = mainPart.Slice(indexOfSlash + 1)
                    let mutable id = 0
                    if Int32.TryParse(stringId, &id)
                    then
                        if "recommend/" == ending
                        then
                            getRecommendedAccounts (id, next, ctx)
                        else
                            if "suggest/" == ending
                            then
                                getSuggestedAccounts (id, next, ctx)
                            else
                                setStatusCode 404 next ctx
                    else
                        setStatusCode 404 next ctx
                else
                    setStatusCode 404 next ctx
            else
                setStatusCode 404 next ctx
let webApp =
    choose [
        GET >=> customGetRoutef
        setStatusCode 404 ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger)=
    Console.WriteLine(ex.ToString())
    setStatusCode 400

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) =
    app.UseRequestCounter webApp
    app.UseGiraffeErrorHandler errorHandler |> ignore
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

let configureKestrel (options : KestrelServerOptions) =
    options.ApplicationSchedulingMode <- Abstractions.Internal.SchedulingMode.Inline
    options.AllowSynchronousIO <- false

let loadData folder =
    let str = Path.Combine(folder,"options.txt")
                   |> File.ReadAllLines
    currentTs <- str.[0]
                   |> Int32.Parse

    let mutable accsCount = 0;
    Directory.EnumerateFiles(folder, "accounts_*.json")
                |> Seq.map (File.ReadAllText >> deserializeObjectUnsafe<AccountsUpd>)
                |> Seq.collect (fun accountsObj -> accountsObj.accounts)
                |> Seq.iter (fun acc ->
                    accounts.[acc.id] <- getAccount acc
                    accsCount <- accsCount + 1
                    )

    let names = citiesDictionary
                |> Seq.sortBy (fun kv -> kv.Key)

    namesSerializeDictionary <- namesDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    citiesSerializeDictionary <- citiesDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    countriesSerializeDictionary <- countriesDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    interestsSerializeDictionary <- interestsDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))

    Console.Write("Accounts {0} ", accsCount)




[<EntryPoint>]
let main argv =
    if Directory.Exists("./data")
    then Directory.Delete("./data",true)
    Directory.CreateDirectory("./data") |> ignore
    if File.Exists("/tmp/data/data.zip")
    then
        File.Copy("/tmp/data/options.txt","./data/options.txt")
        ZipFile.ExtractToDirectory("/tmp/data/data.zip","./data")
    else
        File.Copy("options.txt","./data/options.txt")
        ZipFile.ExtractToDirectory("data.zip","./data")
    loadData "./data"
    GC.Collect(2)
    WebHostBuilder()
        .UseKestrel(Action<KestrelServerOptions> configureKestrel)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(Action<IServiceCollection> configureServices)
        .Build()
        .Run()
    0