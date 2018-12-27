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

let accounts = Dictionary<int, Account>()


let jsonStringValues = StringValues "application/json"

let inline deserializeObjectUnsafe<'a> (str: string) =
    JsonSerializer.Deserialize<'a>(str)

let inline deserializeObject<'a> (str: string) =
    try
        Some <| JsonSerializer.Deserialize<'a>(str)
    with
    | exn ->
        None


type LikesComparer() =
    interface IEqualityComparer<Like> with
        member this.Equals(x,y) = x.id = y.id
        member this.GetHashCode(obj) = obj.id
let likesComparer = new LikesComparer()

type IntReverseComparer() =
    interface IComparer<int> with
        member this.Compare(x,y) = if x > y then -1 else 1
let intReverseComparer = new IntReverseComparer()

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

let postResponseBytes = utf8 "{}"
let inline writePostResponse (code: int) (next : HttpFunc) (ctx: HttpContext) =
    let length = postResponseBytes.Length
    ctx.Response.Headers.["Content-Type"] <- jsonStringValues
    ctx.Response.Headers.ContentLength <- Nullable(int64 length)
    ctx.Response.StatusCode <- code
    task {
        do! ctx.Response.Body.WriteAsync(postResponseBytes, 0, length)
        do! ctx.Response.Body.FlushAsync()
        return! next ctx
    }

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

let inline handleInterests (interests: string[]) (account: Account) =
    account.interests <-
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

let inline handleCity city (account: Account) =
    let mutable cityIndex = 0L
    if citiesDictionary.TryGetValue(city, &cityIndex)
    then
        account.city <- cityIndex
    else
        cityIndex <- getStringWeight city
        citiesDictionary.Add(city, cityIndex)
        account.city <- cityIndex

let inline handleCountry country (account: Account) =
    let mutable countryIndex = 0L
    if countriesDictionary.TryGetValue(country, &countryIndex)
    then
        account.country <- countryIndex
    else
        countryIndex <- getStringWeight country
        countriesDictionary.Add(country, countryIndex)
        account.country <- countryIndex

let inline handleFirstName (fname: string) (account: Account) =
    let mutable nameIndex = 0L
    if namesDictionary.TryGetValue(fname, &nameIndex)
    then
        account.fname <- nameIndex
    else
        nameIndex <- getStringWeight fname
        namesDictionary.Add(fname, nameIndex)
        account.fname <- nameIndex

let inline handleEmail (email: string) (account: Account) =
    let atIndex = email.IndexOf('@', StringComparison.Ordinal)
    let emailDomain = email.Substring(atIndex+1)
    if atIndex >= 0 && emailsDictionary.Add(email)
    then
        if account.email |> isNotNull
        then emailsDictionary.Remove(account.email) |> ignore
        account.email <- email
        account.emailDomain <- emailDomain
    else
        raise (ArgumentOutOfRangeException("Trying add existing email"))

let inline handlePhone (phone: string) (account: Account) =
    let phoneCode =
        if phone |> isNull
        then 0
        else Int32.Parse(phone.Substring(2,3))
    account.phone <- phone
    account.phoneCode <- phoneCode

let inline addLikeToDictionary liker likee likeTs =
    if likesDictionary.ContainsKey(likee) |> not
    then likesDictionary.Add(likee, Dictionary<int, struct(single*int)>())
    let likers = likesDictionary.[likee]
    if likers.ContainsKey(liker)
    then
        let struct(ts, count) = likers.[liker]
        likers.[liker] <- struct(ts + (single)likeTs, count+1)
    else
        likers.[liker] <- struct((single)likeTs, 1)

let handleLikes (likes: Like[]) (account: Account) (deletePrevious: bool) =
    if deletePrevious
    then
        for likeId in account.likes do
            let likers = likesDictionary.[likeId]
            likers.Remove(account.id) |> ignore
    for like in likes do
        addLikeToDictionary account.id like.id like.ts

    account.likes <-
        likes
        |> Array.map(fun like -> like.id)
        |> Array.distinct
        |> Array.sortDescending
        |> ResizeArray

let createAccount (accUpd: AccountUpd): Account =

    let account = Account()
    account.id <- accUpd.id.Value
    handleEmail accUpd.email account
    handlePhone accUpd.phone account
    if accUpd.fname |> isNotNull
    then
        handleFirstName accUpd.fname account
    account.sname <- accUpd.sname
    if accUpd.interests |> isNotNull
    then
        handleInterests accUpd.interests account
    account.status <- getStatus accUpd.status
    account.premium <- accUpd.premium
    account.premiumNow <- box accUpd.premium |> isNotNull && accUpd.premium.start <= currentTs && accUpd.premium.finish > currentTs
    account.sex <- accUpd.sex.[0]
    if accUpd.likes |> isNotNull
    then
        handleLikes accUpd.likes account false
    account.birth <- accUpd.birth.Value
    account.birthYear <- (convertToDate accUpd.birth.Value).Year
    account.joined <- accUpd.joined.Value
    account.joinedYear <- (convertToDate accUpd.joined.Value).Year
    if accUpd.city |> isNotNull
    then
        handleCity accUpd.city account
    if accUpd.country |> isNotNull
    then
        handleCountry accUpd.country account
    account

let updateExistingAccount (existing: Account, accUpd: AccountUpd) =
    if accUpd.birth.HasValue
    then
        existing.birth <- accUpd.birth.Value
        existing.birthYear <- (convertToDate accUpd.birth.Value).Year
    if accUpd.joined.HasValue
    then
        existing.joined <- accUpd.joined.Value
        existing.joinedYear <- (convertToDate accUpd.joined.Value).Year
    if accUpd.sex |> isNotNull
    then
        if accUpd.sex.Length > 1
        then raise (ArgumentOutOfRangeException("Sex is wrong"))
        existing.sex <- accUpd.sex.[0]
    if accUpd.city |> isNotNull
    then
        handleCity accUpd.city existing
    if accUpd.country |> isNotNull
    then
        handleCountry accUpd.country existing
    if accUpd.email |> isNotNull
    then
        handleEmail accUpd.email existing
    if accUpd.phone |> isNotNull
    then
        handlePhone accUpd.phone existing
    if accUpd.fname |> isNotNull
    then
        handleFirstName accUpd.fname existing
    if accUpd.sname |> isNotNull
    then
        existing.sname <- accUpd.sname
    if accUpd.interests |> isNotNull
    then
        handleInterests accUpd.interests existing
    if accUpd.status |> isNotNull
    then
        existing.status <- getStatus accUpd.status
    if box accUpd.premium |> isNotNull
    then
        existing.premium <- accUpd.premium
        existing.premiumNow <- accUpd.premium.start <= currentTs && accUpd.premium.finish > currentTs
    if accUpd.likes |> isNotNull
    then
        handleLikes accUpd.likes existing true
    ()

let getFilteredAccounts (next, ctx : HttpContext) =
    Interlocked.Increment(accountFilterCount) |> ignore
    try
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
        let filters =
            keys
            |> Seq.map (fun key -> filters.[key] ctx.Request.Query.[key].[0])
        let accs =
            filters
            |> Seq.fold (fun acc f -> acc |> Seq.filter f) (seq accounts.Values)
            |> Seq.sortByDescending (fun acc -> acc.id)
            |> Seq.truncate (Int32.Parse(ctx.Request.Query.["limit"].[0]))
        let memoryStream = serializeAccounts (accs, keys)
        writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException ->
        Console.WriteLine("NotSupportedException: " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

let seq_sort value =
    if value = -1
    then Seq.sortByDescending
    else Seq.sortBy

let applyGrouping (memoryStream: byref<MemoryStream>, groupKey, order, accs: Account seq, limit) =
    match groupKey with
    | "sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.sex)
            |> Seq.map (fun (key, group) -> (key, group |> Seq.length))
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsSex(groups, "sex")
    | "status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsStatus(groups, "status")
    | "country" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCountry(groups, "country")
    | "city" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCity(groups, "city")
    | "interests" ->
        let interests =
            accs
            |> Seq.filter (fun acc -> acc.interests |> isNotNull)
            |> Seq.collect (fun acc -> acc.interests)
            |> Seq.groupBy id
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsInterests(interests , "interests")
    | "city,status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city, acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Status(groups, "city", "status")
    | "city,sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city, acc.sex)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "city", "sex")
    | "country,sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country, acc.sex)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "country", "sex")
    | "country,status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country, acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seq_sort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Status(groups, "country", "status")
    | _ ->
        ()


let getGroupedAccounts (next, ctx : HttpContext) =
    Interlocked.Increment(accountsGroupCount) |> ignore
    try
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id" || key =~ "order" || key=~"keys") |> not )
        let filters =
            keys
            |> Seq.map (fun key -> groupFilters.[key] ctx.Request.Query.[key].[0])
        let groupKey =
            ctx.Request.Query.["keys"].[0]
        let order =
            Int32.Parse(ctx.Request.Query.["order"].[0])
        let accs =
            filters
            |> Seq.fold (fun acc f -> acc |> Seq.filter f) (seq accounts.Values)
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

let getCompatibility (target: Account) (acc: Account)  =
    let commonInterestsCount =
        if acc.interests |> isNull || target.interests |> isNull
        then 0
        else acc.interests.Intersect(target.interests).Count()
    if commonInterestsCount = 0
    then
        None
    else
        let statusrank =
            match acc.status with
            | Helpers.freeStatus -> 2
            | Helpers.complexStatus -> 1
            | Helpers.occupiedStatus -> 0
            | _ -> failwith "Invalid status"
        let yearsDifference = 100 - (Math.Abs (acc.birth - target.birth))
        Some (acc.premiumNow, statusrank, commonInterestsCount, yearsDifference, -acc.id)

let recommendationFields = [| "status_eq"; "fname_eq"; "sname_eq"; "birth_year"; "premium_now" |]
let getRecommendedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsRecommendCount) |> ignore
    try
        if (id > accounts.Count)
        then
            setStatusCode 404 next ctx
        else
            let target = accounts.[id]
            let keys =
                ctx.Request.Query.Keys
                |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
                |> Seq.map (fun key ->
                        let value = ctx.Request.Query.[key].[0]
                        if String.IsNullOrEmpty(value)
                        then raise (KeyNotFoundException("Unknown value of get parameter"))
                        (key, value)
                    )
            let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
            if limit < 0 || limit > 20
            then
                setStatusCode 400 next ctx
            else
                let filters =
                    keys
                    |> Seq.map (fun (key, value) -> recommendFilters.[key] value)
                let accounts =
                    accounts.Values
                    |> Seq.filter(fun acc -> acc.sex <> target.sex)
                let accs =
                    filters
                    |> Seq.fold (fun acc f -> acc |> Seq.filter f) accounts
                    |> Seq.map (fun acc -> acc, getCompatibility target acc)
                    |> Seq.filter (fun (acc, compat) -> compat.IsSome)
                    |> Seq.sortByDescending (fun (acc, comp) -> comp.Value)
                    |> Seq.map (fun (acc, comp) -> acc)
                    |> Seq.truncate limit
                let memoryStream = serializeAccounts (accs, recommendationFields)
                writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

let getSimilarityNew targetId (likers: Dictionary<int,struct(single*int)>) (results:Dictionary<int,single>) =
    let struct(targTsSum, count) = likers.[targetId]
    let targTs = targTsSum / (single) count
    for liker in likers do
        if liker.Key <> targetId
        then
            let struct(likerTsSum, count) = liker.Value
            let likerTs = likerTsSum / (single) count
            let currrentSimilarity = 1.0f / Math.Abs(targTs - likerTs)
            if (results.ContainsKey(liker.Key))
            then results.[liker.Key] <- results.[liker.Key] + currrentSimilarity
            else results.[liker.Key] <- currrentSimilarity

let suggestionFields = [| "status_eq"; "fname_eq"; "sname_eq"; |]
let getSuggestedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsSuggestCount) |> ignore
    try
    if (id > accounts.Count || (box accounts.[id] |> isNull) )
    then
        setStatusCode 404 next ctx
    else
        let target = accounts.[id]
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
            |> Seq.map (fun key ->
                    let value = ctx.Request.Query.[key].[0]
                    if String.IsNullOrEmpty(value)
                    then raise (KeyNotFoundException("Unknown value of get parameter"))
                    (key, value)
                )
        let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
        if limit < 0 || limit > 20
        then
            setStatusCode 400 next ctx
        else
            if target.likes |> isNull
            then
                let memoryStream = serializeAccounts ([||], recommendationFields)
                writeResponse memoryStream next ctx
            else
                let filters =
                    keys
                    |> Seq.map (fun (key, value) -> recommendFilters.[key] value)
                let similaritiesWithUsers = Dictionary<int, single>()
                for likeId in target.likes do
                    getSimilarityNew target.id (likesDictionary.[likeId]) similaritiesWithUsers
                let similarAccounts =
                    similaritiesWithUsers.Keys
                    |> Seq.map (fun id -> accounts.[id])
                    |> Seq.filter(fun acc -> acc.sex = target.sex)
                let accs =
                    filters
                    |> Seq.fold (fun acc f -> acc |> Seq.filter f) similarAccounts
                    |> Seq.sortByDescending (fun acc -> similaritiesWithUsers.[acc.id])
                    |> Seq.collect(fun acc -> acc.likes)
                    |> Seq.filter (fun likeId -> target.likes.Contains(likeId) |> not)
                    |> Seq.map (fun likeId -> accounts.[likeId])
                    |> Seq.truncate limit
                let memoryStream = serializeAccounts (accs, suggestionFields)
                writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

let findUser (next, ctx : HttpContext) =
    let likeIds = ctx.Request.Query.["likes"].[0].Split(',') |> Array.map Int32.Parse
    let users =
        accounts.Values
        |> Seq.filter(fun acc -> (box acc) |> isNotNull)
        |> Seq.filter(fun acc -> acc.likes |> isNotNull)
        |> Seq.filter(fun acc -> acc.likes.Intersect(likeIds).Count() >= likeIds.Length)
    json users next ctx

let private accountsFilterString = "/accounts/filter/"
let private accountsGroupString = "/accounts/group/"
let private findUserString = "/findUser/"

let customGetRoutef : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.Request.Path.Value with
        | filterPath when filterPath =~ accountsFilterString ->
             getFilteredAccounts (next, ctx)
        | filterPath when filterPath =~ accountsGroupString ->
             getGroupedAccounts (next, ctx)
        | filterPath when filterPath =~ findUserString ->
             findUser (next, ctx)
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

let private newUserString = "/accounts/new/"
let private newLikesString = "/accounts/likes/"

let newAccount (next, ctx : HttpContext) =
    Interlocked.Increment(newAccountCount) |> ignore
    task {
        try

            let! json = ctx.ReadBodyFromRequestAsync()
            let account = deserializeObjectUnsafe<AccountUpd>(json)
            if account.id.HasValue |> not
            then
                return! setStatusCode 400 next ctx
            else
                accounts.[account.id.Value] <- createAccount account
                return! writePostResponse 201 next ctx
        with
        | :? ArgumentOutOfRangeException ->
            return! setStatusCode 400 next ctx
        | :? JsonParsingException ->
            return! setStatusCode 400 next ctx
    }

let updateAccount (id, next, ctx : HttpContext) =
    Interlocked.Increment(updateAccountCount) |> ignore
    if (id > accounts.Count || (box accounts.[id] |> isNull) )
    then
        setStatusCode 404 next ctx
    else
        task {
            try
                let! json = ctx.ReadBodyFromRequestAsync()
                let account = deserializeObjectUnsafe<AccountUpd>(json)
                if accounts.ContainsKey(id) |> not
                then
                    return! setStatusCode 400 next ctx
                else
                    let target = accounts.[id]
                    updateExistingAccount(target, account)
                    return! writePostResponse 202 next ctx
            with
            | :? ArgumentOutOfRangeException ->
                return! setStatusCode 400 next ctx
            | :? JsonParsingException ->
                return! setStatusCode 400 next ctx
        }

let addLikes (next, ctx : HttpContext) =
    Interlocked.Increment(addLikesCount) |> ignore
    task {
        try
            let! json = ctx.ReadBodyFromRequestAsync()
            let likes = deserializeObjectUnsafe<LikesUpd>(json)
            for like in likes.likes do
                let acc = accounts.[like.liker]
                let likee = accounts.[like.likee]
                if (acc.likes |> isNotNull)
                then acc.likes.Add(like.likee)
                else acc.likes <- ResizeArray(seq { yield like.likee })
                acc.likes.Sort(intReverseComparer)
                addLikeToDictionary like.liker like.likee like.ts
            return! writePostResponse 202 next ctx
        with
        | :? JsonParsingException ->
            return! setStatusCode 400 next ctx
        | :? KeyNotFoundException ->
                return! setStatusCode 400 next ctx
    }

let customPostRoutef : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.Request.Path.Value with
        | filterPath when filterPath =~ newUserString ->
             newAccount (next, ctx)
        | filterPath when filterPath =~ newLikesString ->
             addLikes (next, ctx)
        | filterPath ->
            if filterPath.Length > 10
            then
                let sp = filterPath.AsSpan()
                let mainPart = sp.Slice(10)
                let indexOfSlash = mainPart.IndexOf('/')
                if indexOfSlash > 0
                then
                    let stringId = mainPart.Slice(0,indexOfSlash)
                    let mutable id = 0
                    if Int32.TryParse(stringId, &id)
                    then
                        updateAccount (id, next, ctx)
                    else
                        setStatusCode 404 next ctx
                else
                    setStatusCode 404 next ctx
            else
                setStatusCode 404 next ctx

let webApp =
    choose [
        GET >=> customGetRoutef
        POST >=> customPostRoutef
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
                    accounts.[acc.id.Value] <- createAccount acc
                    accsCount <- accsCount + 1
                    )

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