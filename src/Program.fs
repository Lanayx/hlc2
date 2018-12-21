module HCup.App

open System
open System.Buffers
open System.IO
open System.IO.Compression
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Text
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

[<Literal>]
let LocationsSize = 800000
[<Literal>]
let UsersSize = 1050000
[<Literal>]
let VisitsSize = 10050000



let mutable accounts = [||]

let jsonStringValues = StringValues "application/json"

type UpdateEntity<'a> = 'a -> string -> bool
type IdHandler = int*HttpFunc*HttpContext -> HttpFuncResult

let inline deserializeObjectUnsafe<'a> (str: string) =
    JsonSerializer.Deserialize<'a>(str)

let inline deserializeObject<'a> (str: string) =
    try
        Some <| JsonSerializer.Deserialize<'a>(str)
    with
    | exn ->
        None


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
            |> Array.map (fun (key, group) -> (string key, group.Length))
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups(groups, "sex")
    | "status" -> 
        let groups =
            accs 
            |> Array.groupBy (fun acc -> acc.status)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups(groups, "status")
    | "country" -> 
        let groups =
            accs 
            |> Array.groupBy (fun acc -> acc.country)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups(groups, "country")
    | "city" -> 
        let groups =
            accs 
            |> Array.groupBy (fun acc -> acc.city)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups(groups, "city")
    | "interests" -> 
        let interests =
            accs
            |> Array.filter (fun acc -> acc.interests |> isNotNull)
            |> Array.collect (fun acc -> acc.interests)
            |> Array.groupBy id  
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups(interests , "interests")
    | "city,status" -> 
        let groups =
            accs 
            |> Array.groupBy (fun acc -> acc.city, acc.status)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups2(groups, "city", "status")
    | "city,sex" -> 
        let groups =
            accs 
            |> Array.groupBy (fun acc -> acc.city, string acc.sex)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups2(groups, "city", "sex")
    | "country,sex" -> 
        let groups =
            accs 
            |> Array.groupBy (fun acc -> acc.country, string acc.sex)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups2(groups, "country", "sex")
    | "country,status" -> 
        let groups =
            accs 
            |> Array.groupBy (fun acc -> acc.country, acc.status)
            |> Array.map (fun (key, group) -> key, group.Length)
            |> array_sort order (fun (group,length) -> length, group)
            |> Array.truncate limit
        memoryStream <- serializeGroups2(groups, "country", "status")
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

let getRecommendedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsRecommendCount) |> ignore    
    setStatusCode 401 next ctx
  
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
    accounts <- Directory.EnumerateFiles(folder, "accounts_*.json")
                |> Seq.map (File.ReadAllText >> deserializeObjectUnsafe<Accounts>)
                |> Seq.collect (fun accountsObj -> accountsObj.accounts)
                |> Seq.toArray

    Console.Write("Accounts {0} ", accounts.Length)


    let str = Path.Combine(folder,"options.txt")
                   |> File.ReadAllLines
    currentTs <- str.[0]
                   |> Int32.Parse


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