module Recommend

open HCup.Dictionaries
open HCup
open HCup.Common
open Microsoft.AspNetCore.Http
open BitmapIndex
open System
open System.Collections.Generic
open System.Threading
open HCup.MethodCounter
open HCup.Filters
open HCup.BufferSerializers
open HCup.Requests.Filter
open Giraffe
open System.IO
open HCup.Models

let inline intersectTwoArraysCount (first: byte[]) (second: byte[]) =
    let mutable count = 0
    let mutable i = 0
    let mutable j = 0
    while i < first.Length do
        j <- 0
        while j < second.Length do
            if first.[i] = second.[j]
            then
                count <- count + 1
                j <- second.Length
            else
                j <- j + 1
        i <- i + 1
    count

let getCompatibility (target: Account) (acc: Account)  =
    let commonInterestsCount =
        if acc.interests |> isNull || target.interests |> isNull
        then 0
        else intersectTwoArraysCount acc.interests target.interests
    if commonInterestsCount = 0
    then
        None
    else
        let statusrank =
            match acc.status with
            | Common.freeStatus -> 2
            | Common.complexStatus -> 1
            | Common.occupiedStatus -> 0
            | _ -> failwith "Invalid status"
        let yearsDifference = 100 - (Math.Abs (acc.birth - target.birth))
        Some (acc.premiumNow, statusrank, commonInterestsCount, yearsDifference, -acc.id)

let recommendationFields = [| "status_eq"; "fname_eq"; "sname_eq"; "birth_year"; "premium_now" |]
let getRecommendedAccounts (id, next, ctx : HttpContext) =
    Interlocked.Increment(accountsRecommendCount) |> ignore
    try
        if (id > accountsNumber)
        then
            setStatusCode 404 next ctx
        else
            let target = accounts.[id]
            let keys =
                ctx.Request.Query.Keys
                |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id") |> not )
                |> Seq.sortBy (fun key -> groupFiltersOrder.[key])
                |> Seq.map (fun key ->
                        let value = ctx.Request.Query.[key].[0]
                        if String.IsNullOrEmpty(value)
                        then raise (KeyNotFoundException("Unknown value of get parameter"))
                        (key, value)
                    )
                |> Seq.toArray
            let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
            if limit <= 0 || limit > 20
            then
                setStatusCode 400 next ctx
            else
                let filters =
                    keys
                    |> Seq.map (fun (key, value) -> recommendFilters.[key] value)
                let accounts =
                    getRecommendUsers target.sex
                let accs =
                    filters
                    |> Seq.fold (fun acc f -> acc |> Seq.filter f) accounts
                    |> Seq.map (fun acc -> acc, getCompatibility target acc)
                    |> Seq.filter (fun (acc, compat) -> compat.IsSome)
                    |> Seq.sortByDescending (fun (acc, comp) -> comp.Value)
                    |> Seq.map (fun (acc, comp) -> acc)
                    |> Seq.truncate limit
                let memoryStream = serializeAccounts (accs, recommendationFields)
                writeResponse memoryStream next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx
