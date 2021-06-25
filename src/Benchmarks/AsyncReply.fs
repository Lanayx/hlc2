namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open System

type Message1 =
    | MsgType1 of TaskCompletionSource<int>

type Message2 =
    | MsgType2 of AsyncReplyChannel<int>

[<MemoryDiagnoser>]
type AsyncReplyBenchmarks() =

    let mb1 = MailboxProcessor.Start(fun inbox ->
        let rec loop () =
            async {
                match! inbox.Receive() with
                | MsgType1 ch ->
                    ch.SetResult(1)
                    return! loop()
            }
        loop ()
        )

    let mb2 = MailboxProcessor.Start(fun inbox ->
        let rec loop () =
            async {
                match! inbox.Receive() with
                | MsgType2 ch ->
                    ch.Reply(1)
                    return! loop()
            }
        loop ()
        )

    [<Benchmark>]
    member this.TaskCompletion() =
        task {
            let cts = TaskCompletionSource<int>()
            mb1.Post(MsgType1 cts)
            return! cts.Task
        }

    [<Benchmark>]
    member this.AsyncReply() =
        task {
            return! mb2.PostAndAsyncReply(fun ch -> MsgType2 ch)
        }

//|         Method |       Mean |    Error |   StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
//|--------------- |-----------:|---------:|---------:|-------:|------:|------:|----------:|
//| TaskCompletion |   850.3 ns | 43.61 ns | 123.0 ns | 0.1698 |     - |     - |     711 B |
//|     AsyncReply | 4,171.2 ns | 81.85 ns | 127.4 ns | 0.5722 |     - |     - |    2402 B |