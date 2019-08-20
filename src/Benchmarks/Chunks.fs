namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http

module Chunks =

    let splitByChunks<'a> max (arr: seq<'a>) =
        let mutable finish = false
        let enumer = arr.GetEnumerator()
        if enumer.MoveNext() then
            let newSeq = seq {
                while not finish do
                    let current = enumer.Current
                    if enumer.MoveNext() then
                        yield current
                    else
                        finish <- true
                        yield current
            }
            seq {
                while not finish do
                    yield newSeq |> Seq.truncate max
            }
        else
            Seq.empty

[<MemoryDiagnoser>]
type ChunksBenchmarks() =
    member val arr = Array.init 10000 id

    [<Benchmark>]
    member this.ChunksDefault() =
        this.arr |> Seq.chunkBySize 100 |> Seq.iter (fun x -> Seq.last x |> ignore)

    [<Benchmark>]
    member this.CustomChunks() =
        this.arr |> Chunks.splitByChunks 100 |> Seq.iter (fun x -> Seq.last x |> ignore)

