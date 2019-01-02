namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http

[<MemoryDiagnoser>]
type TraverseBenchmarks() =
    member val l = ["a";"b";"c";"d";"e";"f";"g"]
    member val a = [|"a";"b";"c";"d";"e";"f";"g"|]

    member this.DoSomeWork() =
        [|1..100|] |> Array.rev |> ignore

    [<Benchmark>]
    member this.RecLoop() =
        let rec myRec keys =
            match keys with
            | [] -> 0
            | h::t when h = "g" -> 100
            | h::t when h = "f" -> 200
            | h::t when h = "e" -> 300
            | h::t -> myRec t
        myRec this.l

    [<Benchmark>]
    member this.WhileLoop() =
        let mutable ready = false
        let mutable i = 0
        let mutable result = 0
        while i < this.a.Length && (ready |> not) do
            if (this.a.[i] = "g")
            then
                result <- 100
                ready <- true
            else if (this.a.[i] = "f")
            then
                result <- 200
                ready <- true
            else if (this.a.[i] = "e")
            then
                result <- 300     
                ready <- true       
            i <- i + 1
        result

//    Method |     Mean |     Error |    StdDev | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
//---------- |---------:|----------:|----------:|------------:|------------:|------------:|--------------------:|
//   RecLoop | 66.22 ns | 0.1189 ns | 0.1054 ns |           - |           - |           - |                   - |
// WhileLoop | 68.03 ns | 0.0811 ns | 0.0633 ns |           - |           - |           - |                   - |