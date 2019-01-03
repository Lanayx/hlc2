module HCup.RequestCounter

open System
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Giraffe
open GCTimer

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
                if sw.ElapsedMilliseconds > 25L
                then Console.WriteLine("Slow request {0}ms: {1}", sw.ElapsedMilliseconds, ctx.Request.Path + ctx.Request.QueryString)
                sw.Stop()
        )

type IApplicationBuilder with
    member this.UseRequestCounter (handler : HttpHandler) =
        this.UseMiddleware<RequestCounterMiddleware> handler
        |> ignore
