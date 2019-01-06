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
open HCup.Common
open HCup.BufferSerializers
open HCup.MethodCounter
open HCup.Dictionaries
open HCup.Requests
open Giraffe
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.AspNetCore.Server.Kestrel.Transport
open Utf8Json
open System
open Filters
open Microsoft.Extensions.DependencyInjection
open System.Text.RegularExpressions
open System.Diagnostics
open BitmapIndex
open System.Collections.ObjectModel
open System.Runtime

// ---------------------------------
// Web app
// ---------------------------------

type LikesComparer() =
    interface IEqualityComparer<Like> with
        member this.Equals(x,y) = x.id = y.id
        member this.GetHashCode(obj) = obj.id
let likesComparer = new LikesComparer()

//Test method
let findUser (next, ctx : HttpContext) =
    let likeIds = ctx.Request.Query.["likes"].[0].Split(',') |> Array.map Int32.Parse
    let users =
        accounts
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
            ctx.Items.Add(Common.routeName, Common.filterRoute)
            Filter.getFilteredAccounts (next, ctx)
        | filterPath when filterPath =~ accountsGroupString ->
            ctx.Items.Add(Common.routeName, Common.groupRoute)
            Group.getGroupedAccounts (next, ctx)
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
                            ctx.Items.Add(Common.routeName, Common.recommendRoute)
                            Recommend.getRecommendedAccounts (id, next, ctx)
                        else
                            if "suggest/" == ending
                            then
                                ctx.Items.Add(Common.routeName, Common.suggestRoute)
                                Suggest.getSuggestedAccounts (id, next, ctx)
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

let customPostRoutef : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        shouldRebuildIndex <- true
        match ctx.Request.Path.Value with
        | filterPath when filterPath =~ newUserString ->
            ctx.Items.Add(Common.routeName, Common.newAccountRoute)
            PostAccount.newAccount (next, ctx)
        | filterPath when filterPath =~ newLikesString ->
            ctx.Items.Add(Common.routeName, Common.addLikesRoute)
            PostLikes.addLikes (next, ctx)
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
                        ctx.Items.Add(Common.routeName, Common.updateAccountRoute)
                        PostAccount.updateAccount (id, next, ctx)
                    else
                        setStatusCode 404 next ctx
                else
                    setStatusCode 404 next ctx
            else
                setStatusCode 404 next ctx


let buildBitMapIndex() =
    interestsIndex <- BitmapIndex()
    getAccounts()
    |> Seq.iter (fun account ->
        if account.interests |> isNotNull
        then
            account.interests
            |> Seq.iter (fun interest -> interestsIndex.Set(BIKey(0,interest),account.id)))

let sortGroupDictionaries() =
    Console.WriteLine("Sorting group dictionaries")
    citySexGroups.[0uy] <- citySexGroups.[0uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    citySexGroups.[1uy] <- citySexGroups.[1uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    cityStatusGroups.[0uy] <- cityStatusGroups.[0uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    cityStatusGroups.[1uy] <- cityStatusGroups.[1uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    cityStatusGroups.[2uy] <- cityStatusGroups.[2uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countrySexGroups.[0uy] <- countrySexGroups.[0uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countrySexGroups.[1uy] <- countrySexGroups.[1uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countryStatusGroups.[0uy] <- countryStatusGroups.[0uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countryStatusGroups.[1uy] <- countryStatusGroups.[1uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))
    countryStatusGroups.[2uy] <- countryStatusGroups.[2uy].OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))

    interestGroups <- interestGroups.OrderBy(fun kv -> kv.Value, kv.Key).ToDictionary((fun k -> k.Key), (fun v -> v.Value))

let indexesRebuild() =
    try
        buildBitMapIndex()
        sortGroupDictionaries()
    with
    | :? Exception as ex -> Console.WriteLine("Exception whild building index" + ex.ToString())

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

    let sw = Stopwatch()
    sw.Start()
    Directory.EnumerateFiles(folder, "accounts_*.json")
                |> Seq.map (File.ReadAllText >> deserializeObjectUnsafe<AccountsUpd>)
                |> Seq.iteri (fun i accsUpd ->
                        accsUpd.accounts
                        |> Seq.iter (fun acc ->
                            accounts.[acc.id.Value] <- PostAccount.createAccount acc
                            Interlocked.Increment(&accountsNumber) |> ignore
                        )
                        GC.Collect(2, GCCollectionMode.Forced, true, true)
                        Console.Write("{0}){1}mb:{2}s ", i, Process.GetCurrentProcess().PrivateMemorySize64/MB, sw.Elapsed.TotalSeconds)
                    )
    sw.Stop()
    Console.WriteLine()

    namesSerializeDictionary <- fnamesWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    snamesSerializeDictionary <- snamesWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> struct(kv.Key, utf8 kv.Key)))
    citiesSerializeDictionary <- citiesWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    countriesSerializeDictionary <- countriesWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))
    interestsSerializeDictionary <- interestsWeightDictionary.ToDictionary((fun kv -> kv.Value), (fun kv -> utf8 kv.Key))

    indexesRebuild() |> ignore

    GC.Collect(2, GCCollectionMode.Forced, true, true)
    GC.WaitForPendingFinalizers()
    GCSettings.LargeObjectHeapCompactionMode <- GCLargeObjectHeapCompactionMode.CompactOnce
    GC.Collect(2, GCCollectionMode.Forced, true, true)

    let memSize = Process.GetCurrentProcess().PrivateMemorySize64/MB
    Console.WriteLine("Accounts {0}. Memory used {1}MB", accountsNumber, memSize)
    Console.WriteLine("Dictionaries names={0},cities={1},countries={2},interests={3},snames={4}",
        fnamesWeightDictionary.Count, citiesWeightDictionary.Count,countriesWeightDictionary.Count,interestsWeightDictionary.Count,snamesWeightDictionary.Count)
    // Dictionaries names=108,cities=609,countries=70,interests=90,snames=1638

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
    GCTimer.runTimer indexesRebuild
    WebHostBuilder()
        .UseKestrel(Action<KestrelServerOptions> configureKestrel)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(Action<IServiceCollection> configureServices)
        .Build()
        .Run()
    0