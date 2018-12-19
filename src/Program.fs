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



let inline jsonBuffer (response : MemoryStream) (next : HttpFunc) (ctx: HttpContext) =
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

let inline getExpression (key_pred: string) (value: string) =
    filters.[key_pred] value

let getUser (next, ctx : HttpContext) =
    Interlocked.Increment(accountFilterCount) |> ignore
    let keys = 
        ctx.Request.Query.Keys 
        |> Seq.toArray
        |> Array.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
    let filters =
        keys        
        |> Array.map (fun key -> getExpression key ctx.Request.Query.[key].[0])
    let accs =
        filters
        |> Array.fold (fun acc f -> acc |> Array.filter f) accounts
        |> Array.truncate (Int32.Parse(ctx.Request.Query.["limit"].[0]))
    let memoryStream = serializeAccounts (accs, keys)
    jsonBuffer memoryStream next ctx

let private accountsFilterString = "/accounts/filter/"


let customGetRoutef : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.Request.Path.Value with
        | filterPath when (String.Equals(filterPath, accountsFilterString, StringComparison.Ordinal)) ->
             getUser (next, ctx)
        | _-> setStatusCode 404 next ctx


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
    else ZipFile.ExtractToDirectory("data.zip","./data")
    loadData "./data"
    GC.Collect(2)
    WebHostBuilder()
        .UseKestrel(Action<KestrelServerOptions> configureKestrel)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(Action<IServiceCollection> configureServices)
        .Build()
        .Run()
    0