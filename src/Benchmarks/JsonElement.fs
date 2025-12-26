namespace Benchmarks

open System.Text.Json
open BenchmarkDotNet.Attributes


[<MemoryDiagnoser>]
type JsonElementBenchmarks() =

    let json = """{"storeState":{"messages":[{"role":"user","contents":[{"$type":"text","text":"Tell me a joke about a pirate."}]},{"authorName":"Joker","createdAt":"2025-12-26T03:29:30+00:00","role":"assistant","contents":[{"$type":"text","text":"Why don't pirates take a shower before they walk the plank?\n\nBecause they prefer to **wash up** on shore"}],"messageId":"msg_tmp_3panzldnx17"}]}}"""
    let object = JsonElement.Parse(json)

    [<Benchmark>]
    member this.SystemTextJsonSerialize() =
        JsonSerializer.Serialize(object)

    [<Benchmark>]
    member this.ToStringSerialize() =
        object.ToString()


// |                  Method |      Mean |    Error |   StdDev |   Gen0 | Allocated |
// |------------------------ |----------:|---------:|---------:|-------:|----------:|
// | SystemTextJsonSerialize | 893.98 ns | 7.838 ns | 6.545 ns | 0.0954 |     800 B |
// |       ToStringSerialize |  59.14 ns | 1.129 ns | 1.001 ns | 0.0927 |     776 B |
