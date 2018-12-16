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
open FSharp.Control.Tasks.V2
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.AspNetCore.Server.Kestrel.Transport
open Utf8Json

// ---------------------------------
// Web app
// ---------------------------------

[<Literal>]
let LocationsSize = 800000
[<Literal>]
let UsersSize = 1050000
[<Literal>]
let VisitsSize = 10050000

let mutable currentDate = DateTime.Now
let timestampBase = DateTime(1970, 1, 1, 0, 0, 0, 0)

let accounts = Array.zeroCreate<Account> UsersSize

let jsonStringValues = StringValues "application/json"

type UpdateEntity<'a> = 'a -> string -> bool
type IdHandler = int*HttpFunc*HttpContext -> HttpFuncResult

let inline deserializeObjectUnsafe<'a> (str: string) =
    JsonSerializer.Deserialize<'a>(str)

let inline convertToDate timestamp =
    timestampBase.AddSeconds((float)timestamp)

let inline deserializeObject<'a> (str: string) =
    try
        Some <| JsonSerializer.Deserialize<'a>(str)
    with
    | exn ->
        None



let inline jsonBuffer (response : MemoryStream) =
    fun (next : HttpFunc) (ctx: HttpContext) ->
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

let inline checkStringFromRequest (stringValue: string) =
    stringValue.Contains(": null") |> not

let getUser(id, next, ctx) =
    Interlocked.Increment(getUserCount) |> ignore
    if (id > UsersSize)
    then setStatusCode 404 next ctx
        else
            let user = accounts.[id]
            match box user with
            | null -> setStatusCode 404 next ctx
            | _ -> setStatusCode 404 next ctx  //jsonBuffer (serializeUser user) next ctx



let private usersPathString = "/users"
let private usersPathStringX = PathString("/users")


let customGetRoutef : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let id = ref 0
        match ctx.Request.Path.Value with
        | userPath when (userPath.StartsWith(usersPathString, StringComparison.Ordinal)) ->
                if Int32.TryParse(userPath.Substring(7), id)
                then getUser(id.Value, next, ctx)
                else setStatusCode 404 next ctx
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
    app.UseGiraffe webApp
    app.UseGiraffeErrorHandler errorHandler |> ignore

let configureKestrel (options : KestrelServerOptions) =
    options.ApplicationSchedulingMode <- Abstractions.Internal.SchedulingMode.Inline
    options.AllowSynchronousIO <- false

let loadData folder =



    let accs = Directory.EnumerateFiles(folder, "accounts_*.json")
                |> Seq.map (File.ReadAllText >> deserializeObjectUnsafe<Accounts>)
                |> Seq.collect (fun accountsObj -> accountsObj.accounts)
                |> Seq.map (fun account ->
                    accounts.[account.id] <- account
                    )
                |> Seq.toList
    Console.Write("Accounts {0} ", accs.Length)


    let str = Path.Combine(folder,"options.txt")
                   |> File.ReadAllLines
    currentDate <- str.[0]
                   |> Int64.Parse
                   |> float
                   |> convertToDate


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
        .Build()
        .Run()
    0