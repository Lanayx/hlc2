namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open System.Collections.Generic

module Chunks =

    let splitByChunks<'a> max (arr: seq<'a>) =
        let mutable work = true
        let enumer = arr.GetEnumerator()
        if enumer.MoveNext() then
            let newSeq = seq {
                while work do
                    let current = enumer.Current
                    work <- enumer.MoveNext()
                    yield current
            }
            seq {
                while work do
                    yield newSeq |> Seq.truncate max
            }
        else
            Seq.empty

    let chunkBySize chunkSize (source : seq<_>) =
      seq { use e = source.GetEnumerator()
            let nextChunk() =
                let res = Array.zeroCreate chunkSize
                res.[0] <- e.Current
                let i = ref 1
                while !i < chunkSize && e.MoveNext() do
                    res.[!i] <- e.Current
                    i := !i + 1
                if !i = chunkSize then
                    res
                else
                     Array.sub res 0 !i
            while e.MoveNext() do
                yield nextChunk() }

[<MemoryDiagnoser>]
type ChunksBenchmarks() =
    member val arr = HashSet<int>(Seq.init 5000 id)

    [<Benchmark>]
    member this.ChunksDefault() =
        this.arr |> Seq.chunkBySize 100 |> Seq.iter (fun x -> x |> Seq.iter (fun elem -> ()))

    [<Benchmark>]
    member this.ChunksDefaultSourceCode() =
        this.arr |> Chunks.chunkBySize 100 |> Seq.iter (fun x -> x |> Seq.iter (fun elem -> ()))

    [<Benchmark>]
    member this.CustomChunks() =
        this.arr |> Chunks.splitByChunks 100 |> Seq.iter (fun x -> x |> Seq.iter (fun elem -> ()))

