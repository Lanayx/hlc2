namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open System.Threading
open System.Threading.Tasks
open System.Threading.Channels
open FSharp.Control.Tasks.V2.ContextInsensitive
open System

[<CLIMutable>]
type TestInner =
    {
        TestString: string
        TestInt: int
        TestFloat: float
        TestStrArray: string[]
    }

[<CLIMutable>]
type Test =
    {
        TestInner: TestInner[]
    }

[<MemoryDiagnoser>]
type SerializeBenchmarks() =

    let json = "{\"TestInner\":[{\"TestString\":\"TestString\",\"TestInt\":2,\"TestFloat\":5.1,\"TestStrArray\":[\"one\",\"two\"]}]}"
    let object = {
            TestString = "TestString"
            TestInt = 2
            TestFloat = 5.1
            TestStrArray = [| "one"; "two" |]
        }

    [<Benchmark>]
    member this.SystemTextJsonSerialize() =
        System.Text.Json.JsonSerializer.Serialize(object)

    [<Benchmark>]
    member this.NewtonSoftSerialize() =
        Newtonsoft.Json.JsonConvert.SerializeObject(object)

    [<Benchmark>]
    member this.Utf8JsonSerialize() =
        Utf8Json.JsonSerializer.ToJsonString(json)

    [<Benchmark>]
    member this.SystemTextJsonDeserialize() =
        System.Text.Json.JsonSerializer.Deserialize<Test>(json)

    [<Benchmark>]
    member this.NewtonSoftDeserialize() =
        Newtonsoft.Json.JsonConvert.DeserializeObject<Test>(json)

    [<Benchmark>]
    member this.Utf8JsonDeserialize() =
        Utf8Json.JsonSerializer.Deserialize<Test>(json)


//|                    Method |       Mean |    Error |   StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
//|-------------------------- |-----------:|---------:|---------:|-------:|------:|------:|----------:|
//|   SystemTextJsonSerialize | 1,073.8 ns | 13.34 ns | 12.48 ns | 0.1564 |     - |     - |     656 B |
//|       NewtonSoftSerialize | 1,937.5 ns | 20.92 ns | 19.57 ns | 0.4158 |     - |     - |    1744 B |
//|         Utf8JsonSerialize |   596.1 ns |  3.84 ns |  3.59 ns | 0.0629 |     - |     - |     264 B |
//| SystemTextJsonDeserialize | 2,082.5 ns | 13.53 ns | 12.66 ns | 0.2060 |     - |     - |     872 B |
//|     NewtonSoftDeserialize | 4,492.6 ns | 28.20 ns | 25.00 ns | 0.7706 |     - |     - |    3248 B |
//|       Utf8JsonDeserialize | 1,154.8 ns |  5.36 ns |  5.01 ns | 0.1049 |     - |     - |     440 B |
