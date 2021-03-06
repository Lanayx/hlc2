module HCup.RequestCounter

open System
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Giraffe
open GCTimer
open System.Globalization
open HCup.MethodCounter

let MB = 1024L*1024L

type RequestCounterMiddleware (next : RequestDelegate,
                               handler : HttpHandler) =

    member __.Invoke (ctx : HttpContext) =
        let sw = Stopwatch()
        sw.Start()
        let reqCount = Interlocked.Increment(outstandingRequestCount)
        if (reqCount % 1000 = 0)
        then
            Console.WriteLine("Gen0={0} Gen1={1} Gen2={2} Alloc={3}MB Time={4} ReqCount={5} PrivMemSize={6}MB",
                                    GC.CollectionCount(0),
                                    GC.CollectionCount(1),
                                    GC.CollectionCount(2),
                                    GC.GetTotalMemory(false)/MB,
                                    DateTime.Now.ToString("HH:mm:ss.ffff"),
                                    reqCount,
                                    Process.GetCurrentProcess().PrivateMemorySize64/MB)
        (next.Invoke ctx).ContinueWith(
            fun x ->
                let elapsed = int sw.ElapsedMilliseconds
                match (string ctx.Items.[Common.routeName]) with
                | Common.filterRoute -> Interlocked.Add(accountFilterTime, elapsed) |> ignore
                | Common.groupRoute -> Interlocked.Add(accountsGroupTime, elapsed) |> ignore
                | Common.recommendRoute -> Interlocked.Add(accountsRecommendTime, elapsed) |> ignore
                | Common.suggestRoute -> Interlocked.Add(accountsSuggestTime, elapsed) |> ignore
                | Common.newAccountRoute -> Interlocked.Add(newAccountTime, elapsed) |> ignore
                | Common.updateAccountRoute -> Interlocked.Add(updateAccountTime, elapsed) |> ignore
                | Common.addLikesRoute -> Interlocked.Add(addLikesTime, elapsed) |> ignore
                | _ -> ()

                if elapsed > 25
                then
                    Console.WriteLine("Slow request {0} ms: {1}",
                        elapsed.ToString(CultureInfo.InvariantCulture),
                        ctx.Request.Path + ctx.Request.QueryString)
                sw.Stop()
        )

type IApplicationBuilder with
    member this.UseRequestCounter (handler : HttpHandler) =
        this.UseMiddleware<RequestCounterMiddleware> handler
        |> ignore
