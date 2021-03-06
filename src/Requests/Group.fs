﻿module HCup.Requests.Group

open HCup.Dictionaries
open HCup.Common
open Microsoft.AspNetCore.Http
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

let seqSort order =
    if order = -1
    then Seq.sortByDescending
    else Seq.sortBy

let seqTake order length take =
    if order = -1
    then
        Seq.skip (length - take) >> Seq.rev
    else
        Seq.truncate take

let arrayMinInd (arr: KeyValuePair<int64,int>[]) =
    let mutable minIndex = 0
    for i in [0..arr.Length-1] do
        if arr.[i].Value < arr.[minIndex].Value
            || (arr.[i].Value = arr.[minIndex].Value && arr.[i].Key < arr.[minIndex].Key)
        then
            minIndex <- i
    minIndex

let sortedCollectDict<'T> (sortedSets: KeyValuePair<'T,Dictionary<int64, int>> seq) =
    let setsArray = sortedSets |> Seq.toArray
    let enumerators = setsArray |> Array.map (fun kv -> kv.Value.GetEnumerator())
    let types = setsArray |> Array.map (fun kv -> kv.Key)
    for i in [0..enumerators.Length-1] do
        enumerators.[i].MoveNext() |> ignore
    let firstValues = enumerators |> Array.map (fun en -> en.Current)
    seq {
        let mutable enumerable = enumerators.Length
        while enumerable > 0 do
            let minIndex = firstValues |> arrayMinInd
            let t = types.[minIndex]
            let values = enumerators.[minIndex].Current
            if (enumerators.[minIndex].MoveNext())
            then
                firstValues.[minIndex] <- enumerators.[minIndex].Current
            else
                firstValues.[minIndex] <- KeyValuePair(Int64.MaxValue,Int32.MaxValue)
                enumerable <- enumerable - 1
            yield (values.Key, t), values.Value
    }
    |> Seq.cache

let applyGrouping (memoryStream: byref<MemoryStream>, groupKey, order, accs: Account seq, limit) =
    match groupKey with
    | "sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.sex)
            |> Seq.map (fun (key, group) -> (key, group |> Seq.length))
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsSex(groups, "sex")
    | "status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsStatus(groups, "status")
    | "country" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCountry(groups, "country")
    | "city" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCity(groups, "city")
    | "interests" ->
        let interests =
            accs
            |> Seq.filter (fun acc -> acc.interests |> isNotNull)
            |> Seq.collect (fun acc -> acc.interests)
            |> Seq.groupBy id
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsInterests(interests , "interests")
    | "city,status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city, acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Status(groups, "city", "status", int)
    | "city,sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.city, acc.sex)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "city", "sex", int)
    | "country,sex" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country, acc.sex)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "country", "sex", int)
    | "country,status" ->
        let groups =
            accs
            |> Seq.groupBy (fun acc -> acc.country, acc.status)
            |> Seq.map (fun (key, group) -> key, group |> Seq.length)
            |> seqSort order (fun (group,length) -> length, group)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Status(groups, "country", "status", int)
    | _ ->
        ()

[<Struct>]
type FourthFieldType =
    | NoField
    | Birth of BirthGroup: int16
    | Joined of JoinedGroup : int16

let inline handleGroupFilter<'T> (dict: Dictionary<'T,CountType>) value =
    let mutable count = 0
    if (dict.TryGetValue(value, &count) |> not)
    then 0
    else count

let handleFourthField (count:FourthField) fourthField =
    let struct(cnt, b, j) = count
    match fourthField with
    | NoField ->
        cnt
    | Birth birth ->
        let mutable birthCnt = 0
        if b.TryGetValue(birth, &birthCnt)
        then birthCnt
        else 0
    | Joined joined ->
        let mutable joinedCnt = 0
        if j.TryGetValue(joined, &joinedCnt)
        then joinedCnt
        else 0

let inline handleGroupFilterFF<'T> (dict: Dictionary<'T,FourthField>) value fourthField =
    let mutable count = struct(0,null,null)
    if (dict.TryGetValue(value, &count) |> not)
    then 0
    else
        handleFourthField count fourthField


let inline handleGroupFilterWithWeight (dict: Dictionary<'T,FourthField>) (weightDict: Dictionary<string,'T>) value fourthField =
    let mutable count = struct(0,null,null)
    let mutable weight = Unchecked.defaultof<'T>
    if (weightDict.TryGetValue(value, &weight) |> not) || (dict.TryGetValue(weight, &count) |> not)
    then 0
    else
        handleFourthField count fourthField

let getGroupsWithEmptyFilter (memoryStream: byref<MemoryStream>, groupKey, keys: string list, order, limit, ctx: HttpContext) =
    let mutable fourthField = NoField
    let key =
        if keys.IsEmpty
        then ""
        else
            if keys.Length = 1
            then keys.[0]
            else
                if keys.Length = 2
                then
                    match keys.[0] with
                    | "birth" ->
                        fourthField <- Birth (int16 ctx.Request.Query.[keys.[0]].[0])
                        keys.[1]
                    | "joined" ->
                        fourthField <- Joined (int16 ctx.Request.Query.[keys.[0]].[0])
                        keys.[1]
                    | _ ->
                        match keys.[1] with
                        | "birth" ->
                            fourthField <- Birth (int16 ctx.Request.Query.[keys.[1]].[0])
                            keys.[0]
                        | "joined" ->
                            fourthField <- Joined (int16 ctx.Request.Query.[keys.[1]].[0])
                            keys.[0]
                        | _ ->
                            failwith "Unsupported filters"
                else
                    failwith "Unsupported number of keys"
    match groupKey with
    | "sex" ->
        let groups =
            allSexGroups
            |> Seq.map(fun kv ->
                let struct(i,b,j,ci,co,st) = kv.Value
                if key =~ ""
                then
                    kv.Key, (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "interests" ->
                            handleGroupFilterWithWeight i interestsWeightDictionary value fourthField
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "city" ->
                            handleGroupFilterWithWeight ci citiesWeightDictionary value fourthField
                        | "country" ->
                            handleGroupFilterWithWeight co countriesWeightDictionary value fourthField
                        | "status" ->
                            handleGroupFilterFF st (getStatus value) fourthField
                        | _ -> failwith "Unknown sex filter"
                    kv.Key, count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsSex(groups, "sex")
    | "status" ->
        let groups =
            allStatusGroups
            |> Seq.map(fun kv ->
                let struct(i,b,j,ci,co,s) = kv.Value
                if key =~ ""
                then
                    kv.Key, (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "interests" ->
                            handleGroupFilterWithWeight i interestsWeightDictionary value fourthField
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "city" ->
                            handleGroupFilterWithWeight ci citiesWeightDictionary value fourthField
                        | "country" ->
                            handleGroupFilterWithWeight co countriesWeightDictionary value fourthField
                        | "sex" ->
                            handleGroupFilterFF s (getSex value) fourthField
                        | _ -> failwith "Unknown sex filter"
                    kv.Key, count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsStatus(groups, "status")
    | "country" ->
        let groups =
            allCountryGroups
            |> Seq.map(fun kv ->
                let struct(i,b,j,s,st) = kv.Value
                if key =~ ""
                then
                    kv.Key, (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "interests" ->
                            handleGroupFilterWithWeight i interestsWeightDictionary value fourthField
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "sex" ->
                            handleGroupFilterFF s (getSex value) fourthField
                        | "status" ->
                            handleGroupFilterFF st (getStatus value) fourthField
                        | _ -> failwith "Unknown country filter"
                    kv.Key, count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCountry(groups, "country")
    | "city" ->
        let groups =
            allCityGroups
            |> Seq.map(fun kv ->
                let struct(i,b,j,s,st) = kv.Value
                if key =~ ""
                then
                    kv.Key, (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "interests" ->
                            handleGroupFilterWithWeight i interestsWeightDictionary value fourthField
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "sex" ->
                            handleGroupFilterFF s (getSex value) fourthField
                        | "status" ->
                            handleGroupFilterFF st (getStatus value) fourthField
                        | _ -> failwith "Unknown city filter"
                    kv.Key, count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsCity(groups, "city")
    | "interests" ->
        let groups =
            allInterestsGroups
            |> Seq.map(fun kv ->
                let struct(b,j,ci,co,s,st) = kv.Value
                if key =~ ""
                then
                    kv.Key, (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "city" ->
                            handleGroupFilterWithWeight ci citiesWeightDictionary value fourthField
                        | "country" ->
                            handleGroupFilterWithWeight co countriesWeightDictionary value fourthField
                        | "sex" ->
                            handleGroupFilterFF s (getSex value) fourthField
                        | "status" ->
                            handleGroupFilterFF st (getStatus value) fourthField
                        | _ -> failwith "Unknown interests filter"
                    kv.Key, count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroupsInterests(groups , "interests")
    | "city,status" ->
        let groups =
            allCityStatusGroups
            |> Seq.map(fun kv ->
                let struct(i,b,j,s) = kv.Value
                if key =~ ""
                then
                    kv.Key.ToTuple(), (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "interests" ->
                            handleGroupFilterWithWeight i interestsWeightDictionary value fourthField
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "sex" ->
                            handleGroupFilterFF s (getSex value) fourthField
                        | "status" ->
                            let struct(_, status) = kv.Key
                            if status = (getStatus value)
                            then
                                match fourthField with
                                | NoField ->
                                     b.Values |> Seq.sum
                                | Birth birth ->
                                    let mutable birthCnt = 0
                                    if b.TryGetValue(birth, &birthCnt)
                                    then birthCnt
                                    else 0
                                | Joined joined ->
                                    let mutable joinedCnt = 0
                                    if j.TryGetValue(joined, &joinedCnt)
                                    then joinedCnt
                                    else 0
                            else 0
                        | _ -> failwith "Unknown citystatus filter"
                    kv.Key.ToTuple(), count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Status(groups, "city", "status", int)
    | "city,sex" ->
        let groups =
            allCitySexGroups
            |> Seq.map(fun kv ->
                let struct(i,b,j,st) = kv.Value
                if key =~ ""
                then
                    kv.Key.ToTuple(), (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "interests" ->
                            handleGroupFilterWithWeight i interestsWeightDictionary value fourthField
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "status" ->
                            handleGroupFilterFF st (getStatus value) fourthField
                        | "sex" ->
                            let struct(_, sex) = kv.Key
                            if sex = (getSex value)
                            then
                                match fourthField with
                                | NoField ->
                                     b.Values |> Seq.sum
                                | Birth birth ->
                                    let mutable birthCnt = 0
                                    if b.TryGetValue(birth, &birthCnt)
                                    then birthCnt
                                    else 0
                                | Joined joined ->
                                    let mutable joinedCnt = 0
                                    if j.TryGetValue(joined, &joinedCnt)
                                    then joinedCnt
                                    else 0
                            else 0
                        | _ -> failwith "Unknown citysex filter"
                    kv.Key.ToTuple(), count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "city", "sex", int)
    | "country,sex" ->
        let groups =
            allCountrySexGroups
            |> Seq.map(fun kv ->
                let struct(i,b,j,st) = kv.Value
                if key =~ ""
                then
                    kv.Key.ToTuple(), (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "interests" ->
                            handleGroupFilterWithWeight i interestsWeightDictionary value fourthField
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "status" ->
                            handleGroupFilterFF st (getStatus value) fourthField
                        | "sex" ->
                            let struct(_, sex) = kv.Key
                            if sex = (getSex value)
                            then
                                match fourthField with
                                | NoField ->
                                     b.Values |> Seq.sum
                                | Birth birth ->
                                    let mutable birthCnt = 0
                                    if b.TryGetValue(birth, &birthCnt)
                                    then birthCnt
                                    else 0
                                | Joined joined ->
                                    let mutable joinedCnt = 0
                                    if j.TryGetValue(joined, &joinedCnt)
                                    then joinedCnt
                                    else 0
                            else 0
                        | _ -> failwith "Unknown countrysex filter"
                    kv.Key.ToTuple(), count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Sex(groups, "country", "sex", int)
    | "country,status" ->
        let groups =
            allCountryStatusGroups
            |> Seq.map(fun kv ->
                let struct(i,b,j,s) = kv.Value
                if key =~ ""
                then
                    kv.Key.ToTuple(), (b.Values |> Seq.sum)
                else
                    let value = ctx.Request.Query.[key].[0]
                    let count =
                        match key with
                        | "interests" ->
                            handleGroupFilterWithWeight i interestsWeightDictionary value fourthField
                        | "birth" ->
                            handleGroupFilter b (Int16.Parse(value))
                        | "joined" ->
                            handleGroupFilter j (Int16.Parse(value))
                        | "sex" ->
                            handleGroupFilterFF s (getSex value) fourthField
                        | "status" ->
                            let struct(_, status) = kv.Key
                            if status = (getStatus value)
                            then
                                match fourthField with
                                | NoField ->
                                     b.Values |> Seq.sum
                                | Birth birth ->
                                    let mutable birthCnt = 0
                                    if b.TryGetValue(birth, &birthCnt)
                                    then birthCnt
                                    else 0
                                | Joined joined ->
                                    let mutable joinedCnt = 0
                                    if j.TryGetValue(joined, &joinedCnt)
                                    then joinedCnt
                                    else 0
                            else 0
                        | _ -> failwith "Unknown citystatus filter"
                    kv.Key.ToTuple(), count
                )
            |> Seq.filter (fun (_, count) -> count > 0)
            |> seqSort order (fun (field, count) -> count, field)
            |> Seq.truncate limit
        memoryStream <- serializeGroups2Status(groups, "country", "status", int)
    | _ ->
        ()

let rec getGroupedAccountsByQuery keys (ctx : HttpContext) =
    match keys with
    | [] -> getAccounts()
    | h::t when h = "likes" -> getLikesContainsAccount (ctx.Request.Query.["likes"].[0])
    | h::t -> getGroupedAccountsByQuery t ctx

let getGroupedAccounts (next, ctx : HttpContext) =
    Interlocked.Increment(accountsGroupCount) |> ignore
    try
        let keys =
            ctx.Request.Query.Keys
            |> Seq.filter(fun key -> (key =~ "limit" || key =~ "query_id" || key =~ "order" || key=~"keys") |> not )
            |> Seq.toList
        let limit = Int32.Parse(ctx.Request.Query.["limit"].[0])
        let groupKey =
            ctx.Request.Query.["keys"].[0]
        let order =
                Int32.Parse(ctx.Request.Query.["order"].[0])
        let mutable memoryStream: MemoryStream = null
        if keys.Length = 0 || (keys |> List.contains "likes" |> not)
        then
            getGroupsWithEmptyFilter(&memoryStream, groupKey, keys, order, limit, ctx)
        else
            let filters =
                keys
                |> Seq.sortBy (fun key -> groupFiltersOrder.[key])
                |> Seq.map (fun key -> groupFilters.[key] ctx.Request.Query.[key].[0])
            let accounts =
                getGroupedAccountsByQuery keys ctx
            let accs =
                filters
                |> Seq.fold (fun acc f -> acc |> Seq.filter f) accounts
            applyGrouping(&memoryStream, groupKey, order, accs, limit)
        if (memoryStream |> isNotNull)
        then writeResponse memoryStream next ctx
        else setStatusCode 400 next ctx
    with
    | :? KeyNotFoundException -> setStatusCode 400 next ctx
    | :? FormatException -> setStatusCode 400 next ctx
    | :? NotSupportedException as ex ->
        Console.WriteLine("NotSupportedException:" + ex.Message + " " + ctx.Request.Path + ctx.Request.QueryString.Value)
        setStatusCode 400 next ctx