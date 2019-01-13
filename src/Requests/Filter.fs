module HCup.Requests.Filter

open HCup.Dictionaries
open HCup.Common
open Microsoft.AspNetCore.Http
open BitmapIndex
open System
open System.Collections.Generic
open System.Threading
open HCup.MethodCounter
open HCup.Filters
open HCup.BufferSerializers
open Giraffe


let arrayMaxWithInd (arr: int[]) =
    let mutable max = 0
    let mutable maxIndex = 0
    for i in [0..arr.Length-1] do
        if arr.[i] > max
        then
            max <- arr.[i]
            maxIndex <- i
    struct(max, maxIndex)

let sortedReverseCollect (sortedSets: SortedSet<int> seq) =
    let setsArray = sortedSets |> Seq.toArray
    let enumerators = setsArray |> Array.map (fun set -> set.GetEnumerator())
    for i in [0..enumerators.Length-1] do
        enumerators.[i].MoveNext() |> ignore
    let firstValues = enumerators |> Array.map (fun en -> en.Current)
    seq {
        let mutable enumerable = enumerators.Length
        while enumerable > 0 do
            let struct(max, maxIndex) = firstValues |> arrayMaxWithInd
            if (enumerators.[maxIndex].MoveNext())
            then
                firstValues.[maxIndex] <- enumerators.[maxIndex].Current
            else
                firstValues.[maxIndex] <- 0
                enumerable <- enumerable - 1
            yield max
    }
    |> Seq.cache

let getCityAnyAccounts (value: string) =
    let values = value.Split(',')
    if (value.Length <> 1)
    then
        values
        |> Seq.filter (fun key -> citiesWeightDictionary.ContainsKey(key))
        |> Seq.map (fun key -> citiesWeightDictionary.[key])
        |> Seq.map (fun weight -> citiesIndex.[weight])
        |> sortedReverseCollect
        |> Seq.map (fun id -> accounts.[id])
    else
        if citiesWeightDictionary.ContainsKey(value) |> not
        then
            Seq.empty
        else
            let weight = citiesWeightDictionary.[value]
            citiesIndex.[weight]
            |> Seq.map (fun id -> accounts.[id])

let getFnameAnyAccounts (value: string) =
    let values = value.Split(',')
    if (value.Length <> 1)
    then
        values
        |> Seq.filter (fun key -> fnamesWeightDictionary.ContainsKey(key))
        |> Seq.map (fun key -> fnamesWeightDictionary.[key])
        |> Seq.map (fun weight -> fnamesIndex.[weight])
        |> sortedReverseCollect
        |> Seq.map (fun id -> accounts.[id])
    else
        if fnamesWeightDictionary.ContainsKey(value) |> not
        then
            Seq.empty
        else
            let weight = fnamesWeightDictionary.[value]
            fnamesIndex.[weight]
            |> Seq.map (fun id -> accounts.[id])

let getInterestContainsAccounts (str: string) =
    let mutable criteria = Unchecked.defaultof<BICriteria>
    for value in str.Split(',') do
        let interest = interestsWeightDictionary.[value]
        if criteria |> isNull
        then
            criteria <- BICriteria.equals(BIKey(0, interest))
        else
            criteria <- criteria.``and``(BICriteria.equals(BIKey(0, interest)))
    let result = interestsIndex.query(criteria).GetPositions()
    seq{
        for i = result.Count-1 downto 0 do
            yield accounts.[result.[i]]
    }

let getInterestAnyAccounts (str: string) =
    let mutable criteria = Unchecked.defaultof<BICriteria>
    for value in str.Split(',') do
        let interest = interestsWeightDictionary.[value]
        if criteria |> isNull
        then
            criteria <- BICriteria.equals(BIKey(0, interest))
        else
            criteria <- criteria.``or``(BICriteria.equals(BIKey(0, interest)))
    let result = interestsIndex.query(criteria).GetPositions()
    seq{
        for i = result.Count-1 downto 0 do
            yield accounts.[result.[i]]
    }

let inline getLikesContainsAccount value =
    let key = Int32.Parse(value)
    if likesIndex.[key] |> isNotNull
    then
        seq likesIndex.[key].Keys
    else
        Seq.empty
    |> Seq.map (fun id -> accounts.[id])

let getLikesContainsAccounts (value: string) =
    let values = value.Split(',')
    if (value.Length <> 1)
    then
        let keys =
            values
            |> Seq.map (fun str -> Int32.Parse(str))
            |> Seq.cache
        if Seq.forall (fun key -> likesIndex.[key] |> isNotNull) keys
        then
            Seq.collect (fun key -> likesIndex.[key].Keys) keys
        else
            Seq.empty
        |> Seq.countBy id
        |> Seq.filter (fun (id, count) -> count = values.Length)
        |> Seq.map (fun (id, _) -> accounts.[id])
    else
        getLikesContainsAccount value

let getCityEqAccounts (value: string) =
    let mutable weight = 0s
    if citiesWeightDictionary.TryGetValue(value, &weight)
    then
        citiesIndex.[weight]
        |> Seq.map (fun id -> accounts.[id])
    else
        Seq.empty

let getCountryEqAccounts (value: string) =
    let mutable weight = 0uy
    if countriesWeightDictionary.TryGetValue(value, &weight)
    then
        countriesIndex.[weight]
        |> Seq.map (fun id -> accounts.[id])
    else
        Seq.empty

let rec getFilteredAccountsByQuery keys (ctx : HttpContext) =
    match keys with
    | [] -> getRevAccounts()
    | h::t when h = "likes_contains" -> getLikesContainsAccounts (ctx.Request.Query.["likes_contains"].[0])
    | h::t when h = "interests_contains" -> getInterestContainsAccounts (ctx.Request.Query.["interests_contains"].[0])
    | h::t when h = "interests_any" -> getInterestAnyAccounts (ctx.Request.Query.["interests_any"].[0])
    | h::t when h = "city_any" -> getCityAnyAccounts (ctx.Request.Query.["city_any"].[0])
    | h::t when h = "fname_any" -> getFnameAnyAccounts (ctx.Request.Query.["fname_any"].[0])
    | h::t when h = "city_eq" -> getCityEqAccounts (ctx.Request.Query.["city_eq"].[0])
    | h::t when h = "country_eq" -> getCountryEqAccounts (ctx.Request.Query.["country_eq"].[0])
    | h::t -> getFilteredAccountsByQuery t ctx

let getFilteredAccounts (next, ctx : HttpContext) =
    Interlocked.Increment(accountFilterCount) |> ignore
    try
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
            |> Seq.toList
        let filters =
            keys
            |> Seq.sortBy (fun key -> filtersOrder.[key])
            |> Seq.map (fun key -> filters.[key] ctx.Request.Query.[key].[0])
        let accounts =
            getFilteredAccountsByQuery keys ctx
        let accs =
            filters
            |> Seq.fold (fun acc f -> acc |> Seq.filter f) accounts
            |> Seq.truncate (Int32.Parse(ctx.Request.Query.["limit"].[0]))
        let memoryStream = serializeAccounts (accs, keys)
        writeResponse memoryStream next ctx
    with
    | :? IndexOutOfRangeException ->
        setStatusCode 400 next ctx
    | :? KeyNotFoundException ->
        setStatusCode 400 next ctx
    | :? FormatException ->
        setStatusCode 400 next ctx
    | :? NotSupportedException ->
        Console.WriteLine("NotSupportedException: " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx

