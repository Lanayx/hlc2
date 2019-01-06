namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open System.Collections.Generic

[<MemoryDiagnoser>]
type DictionaryBenchmarks() =
    let _refDictionary = Dictionary<(char*int*int64*int64*int64),int>()
    let _valDictionary = Dictionary<struct(char*int*int64*int64*int64),int>()
    do
        [1..10000] |> List.iter(fun i -> _refDictionary.Add((char i,i,int64 i,int64 i,int64 i),i) |> ignore)
        [1..10000] |> List.iter(fun i -> _valDictionary.Add(struct(char i,i,int64 i,int64 i,int64 i),i) |> ignore)

    member this.refDictionary with get() = _refDictionary
    member this.valDictionary with get() = _valDictionary

    [<Benchmark>]
    member this.GetElementInRefDictionary() =
        this.refDictionary.ContainsKey(char 100,100,100L,100L,1L)
            &&  this.refDictionary.ContainsKey(char 100,100,100L,100L,100L)

    [<Benchmark>]
    member this.GetElementInValDictionary() =
        this.valDictionary.ContainsKey struct(char 100,100,100L,100L,1L)
            &&  this.valDictionary.ContainsKey struct(char 100,100,100L,100L,100L)

//                    Method |      Mean |     Error |    StdDev | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
//-------------------------- |----------:|----------:|----------:|------------:|------------:|------------:|--------------------:|
// GetElementInRefDictionary | 195.44 ns | 0.7094 ns | 0.6288 ns |      0.0401 |           - |           - |               168 B |
// GetElementInValDictionary |  25.10 ns | 0.0917 ns | 0.0857 ns |           - |           - |           - |                   - |