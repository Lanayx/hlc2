namespace Benchmarks

open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http

type RouteBenchmarks() =
    member val accountsFilterString = "/accounts/filter/"
    member val accountsFilterStringZ = "/z/accounts/filter/"
    member val accountsFilterStringX = PathString("/accounts/filter/")
    member val accountsFilterStringXZ = PathString("/z/accounts/filter/")
    member val a = 100


    [<Benchmark>]
    member this.PathStringCompare() =
        let x = PathString("/accounts/filter/")
        match x with
        | m when m = this.accountsFilterStringXZ -> false
        | z when z = this.accountsFilterStringX -> true



    [<Benchmark>]
    member this.StringCompare() =
        let x = PathString("/accounts/filter/")
        match x.Value with
        | m when m = this.accountsFilterStringZ -> false
        | z when z = this.accountsFilterString -> true

