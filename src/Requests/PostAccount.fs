module HCup.Requests.PostAccount

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
open HCup.Requests
open FSharp.Control.Tasks.V2.ContextInsensitive
open Utf8Json

let getStringWeight (str: string) =
    let strChars = str |> Seq.truncate 11
    let mutable multiplier = 0L
    let mutable divisor = 0L
    if (int)str.[0] > 200
    then
        multiplier <- 36028797018963968L //32^11
        divisor <- 32L
    else
        multiplier <- 1521681143169024L // 24^11
        divisor <- 24L
    let mutable result = 0L
    for chr in strChars do
        let intChr = int chr
        let diff =
            match intChr with
            | smallRussianLetter when smallRussianLetter >= 1072 -> intChr - 1070 + 10
            | bigRussianLetter when bigRussianLetter >= 1040 -> intChr - 1040 + 10
            | smallEnglishLetter when smallEnglishLetter >= 97 -> intChr - 97 + 20 // hack to compare Pitbull with PS3
            | bigEnglishLetter when bigEnglishLetter >= 65 -> intChr - 65 + 10
            | number when number >= 48 -> intChr - 47
            | _ -> intChr - 32
        result <- result + (int64 diff) * multiplier
        multiplier <- multiplier / divisor
    result

let inline handleInterests (interests: string[]) (account: Account) =
    account.interests <-
        interests
        |> Array.map(fun interest ->
                let mutable weight = 0L
                if interestsWeightDictionary.TryGetValue(interest, &weight)
                then
                    weight
                else
                    weight <- getStringWeight interest
                    interestsWeightDictionary.Add(interest, weight)
                    interestsSerializeDictionary.Add(weight, utf8 interest)
                    weight
            )

let updateInterestIndex oldInterests (account: Account) (deletePrevious: bool) =
    if deletePrevious && oldInterests |> isNotNull
    then
        oldInterests
        |> Array.iter(fun oldInterestWeight ->
                let oldInterestCount = interestGroups.[oldInterestWeight]
                if oldInterestCount > 1
                then
                    interestGroups.[oldInterestWeight] <- oldInterestCount - 1
                else
                    interestGroups.Remove(oldInterestWeight) |> ignore
            )
    if account.interests |> isNotNull
    then
        account.interests
            |> Array.iter(fun interestWeight ->
                    let mutable interestCount: int = 0
                    if interestGroups.TryGetValue(interestWeight, &interestCount)
                    then
                        interestGroups.[interestWeight] <- interestCount + 1
                    else
                        interestGroups.[interestWeight] <- 1
                )

let inline increaseCounter<'T> (dict: Dictionary<'T,CountType>) (key: 'T) =
    let mutable count = 0;
    if dict.TryGetValue(key, &count)
    then dict.[key] <- count + 1
    else dict.[key] <- 1

let inline decreaseCounter<'T> (dict: Dictionary<'T,CountType>) (key: 'T) =
    let count = dict.[key];
    if count > 1
    then dict.[key] <- count - 1
    else dict.Remove(key) |> ignore

let updateCityIndex oldSex oldStatus oldCity oldInterests oldBirth oldJoined (account: Account) (deletePrevious: bool) =
    if deletePrevious
    then
        let struct(i,b,j,st) = allCitySexGroups.[struct(oldCity,oldSex)]
        for interest in oldInterests do
            decreaseCounter i interest
        decreaseCounter b oldBirth
        decreaseCounter j oldJoined
        decreaseCounter st oldStatus
        let struct(i,b,j,s) = allCityStatusGroups.[struct(oldCity,oldStatus)]
        for interest in oldInterests do
            decreaseCounter i interest
        decreaseCounter b oldBirth
        decreaseCounter j oldJoined
        decreaseCounter s oldSex

    let mutable sexDictionaries = struct(null,null,null,null)
    let key = struct(account.city,account.sex)
    if allCitySexGroups.TryGetValue(key, &sexDictionaries) |> not
    then
        sexDictionaries <- struct(Dictionary<InterestsGroup,CountType>(),Dictionary<BirthGroup,CountType>(),Dictionary<JoinedGroup,CountType>(),Dictionary<StatusGroup,CountType>())
        allCitySexGroups.[key] <- sexDictionaries
    let struct(i,b,j,st) = sexDictionaries
    if account.interests |> isNotNull
    then
        for interest in account.interests do
            increaseCounter i interest
    increaseCounter b account.birthYear
    increaseCounter j account.joinedYear
    increaseCounter st account.status
    let mutable statusDictionaries = struct(null,null,null,null)
    let key = struct(account.city,account.status)
    if allCityStatusGroups.TryGetValue(key, &statusDictionaries) |> not
    then
        statusDictionaries <- struct(Dictionary<InterestsGroup,CountType>(),Dictionary<BirthGroup,CountType>(),Dictionary<JoinedGroup,CountType>(),Dictionary<StatusGroup,CountType>())
        allCityStatusGroups.[key] <- statusDictionaries
    let struct(i,b,j,s) = statusDictionaries
    if account.interests |> isNotNull
    then
        for interest in account.interests do
            increaseCounter i interest
    increaseCounter b account.birthYear
    increaseCounter j account.joinedYear
    increaseCounter s account.sex

let updateCountryIndex oldSex oldStatus oldCountry oldInterests oldBirth oldJoined (account: Account) (deletePrevious: bool) =
    if deletePrevious
    then
        let struct(i,b,j,st) = allCountrySexGroups.[struct(oldCountry,oldSex)]
        for interest in oldInterests do
            decreaseCounter i interest
        decreaseCounter b oldBirth
        decreaseCounter j oldJoined
        decreaseCounter st oldStatus
        let struct(i,b,j,s) = allCountryStatusGroups.[struct(oldCountry,oldStatus)]
        for interest in oldInterests do
            decreaseCounter i interest
        decreaseCounter b oldBirth
        decreaseCounter j oldJoined
        decreaseCounter s oldSex

    let mutable sexDictionaries = struct(null,null,null,null)
    let key = struct(account.country,account.sex)
    if allCountrySexGroups.TryGetValue(key, &sexDictionaries) |> not
    then
        sexDictionaries <- struct(Dictionary<InterestsGroup,CountType>(),Dictionary<BirthGroup,CountType>(),Dictionary<JoinedGroup,CountType>(),Dictionary<StatusGroup,CountType>())
        allCountrySexGroups.[key] <- sexDictionaries
    let struct(i,b,j,st) = sexDictionaries
    if account.interests |> isNotNull
    then
        for interest in account.interests do
            increaseCounter i interest
    increaseCounter b account.birthYear
    increaseCounter j account.joinedYear
    increaseCounter st account.status
    let mutable statusDictionaries = struct(null,null,null,null)
    let key = struct(account.country,account.status)
    if allCountryStatusGroups.TryGetValue(key, &statusDictionaries) |> not
    then
        statusDictionaries <- struct(Dictionary<InterestsGroup,CountType>(),Dictionary<BirthGroup,CountType>(),Dictionary<JoinedGroup,CountType>(),Dictionary<StatusGroup,CountType>())
        allCountryStatusGroups.[key] <- statusDictionaries
    let struct(i,b,j,s) = statusDictionaries
    if account.interests |> isNotNull
    then
        for interest in account.interests do
            increaseCounter i interest
    increaseCounter b account.birthYear
    increaseCounter j account.joinedYear
    increaseCounter s account.sex

let handleCity city (account: Account) (deletePrevious: bool) =
    if deletePrevious && account.city > 0L
    then
        citiesIndex.[account.city].Remove(account.id) |> ignore
    let mutable weight = 0L
    if citiesWeightDictionary.TryGetValue(city, &weight)
    then
        account.city <- weight
    else
        weight <- getStringWeight city
        citiesWeightDictionary.Add(city, weight)
        citiesSerializeDictionary.Add(weight, utf8 city)
        account.city <- weight
    let mutable cityUsers: SortedSet<Int32> = null
    if citiesIndex.TryGetValue(account.city, &cityUsers)
    then
        cityUsers.Add(account.id) |> ignore
    else
        cityUsers <- SortedSet(intReverseComparer)
        cityUsers.Add(account.id) |> ignore
        citiesIndex.[account.city] <- cityUsers

let inline handleCountry country (account: Account) (deletePrevious: bool) =
    if deletePrevious && account.country > 0L
    then
        countriesIndex.[account.country].Remove(account.id) |> ignore
    let mutable weight = 0L
    if countriesWeightDictionary.TryGetValue(country, &weight)
    then
        account.country <- weight
    else
        weight <- getStringWeight country
        countriesWeightDictionary.Add(country, weight)
        countriesSerializeDictionary.Add(weight, utf8 country)
        account.country <- weight

    if account.country > 0L
    then
        let mutable countryUsers: SortedSet<Int32> = null
        if countriesIndex.TryGetValue(account.country, &countryUsers)
        then
            countryUsers.Add(account.id) |> ignore
        else
            countryUsers <- SortedSet(intReverseComparer)
            countryUsers.Add(account.id) |> ignore
            countriesIndex.[account.country] <- countryUsers

let inline handleFirstName (fname: string) (account: Account) (deletePrevious: bool) =
    if deletePrevious && account.fname > 0L
    then
        fnamesIndex.[account.fname].Remove(account.id) |> ignore
    let mutable weight = 0L
    if fnamesWeightDictionary.TryGetValue(fname, &weight)
    then
        account.fname <- weight
    else
        weight <- getStringWeight fname
        fnamesWeightDictionary.Add(fname, weight)
        namesSerializeDictionary.Add(weight, utf8 fname)
        account.fname <- weight

    if account.fname > 0L
    then
        let mutable fnameUsers: SortedSet<Int32> = null
        if fnamesIndex.TryGetValue(account.fname, &fnameUsers)
        then
            fnameUsers.Add(account.id) |> ignore
        else
            fnameUsers <- SortedSet(intReverseComparer)
            fnameUsers.Add(account.id) |> ignore
            fnamesIndex.[account.fname] <- fnameUsers

let inline handleSecondName (sname: string) (account: Account) =
    let mutable weight = 0L
    if snamesWeightDictionary.TryGetValue(sname, &weight)
    then
        account.sname <- weight
    else
        weight <- getStringWeight sname
        snamesWeightDictionary.Add(sname, weight)
        snamesSerializeDictionary.Add(weight, struct(sname,utf8 sname))
        account.sname <- weight

let inline handleEmail (email: string) (account: Account) =
    let atIndex = email.IndexOf('@', StringComparison.Ordinal)
    let emailDomain = email.Substring(atIndex+1)
    if atIndex >= 0 && emailsDictionary.Contains(email) |> not
    then
        if account.email |> isNotNull
        then emailsDictionary.Remove(account.email) |> ignore
        account.email <- email
        account.emailDomain <- emailDomain
    else
        raise (ArgumentOutOfRangeException("Invalid email"))

let inline handlePhone (phone: string) (account: Account) =
    let phoneCode =
        if phone |> isNull
        then 0
        else Int32.Parse(phone.Substring(2,3))
    account.phone <- phone
    account.phoneCode <- phoneCode

let inline addLikeToDictionary liker likee likeTs =
    if likesIndex.[likee] |> isNull
    then likesIndex.[likee] <- SortedList<int, struct(single*byte)>(intReverseComparer)
    let likers = likesIndex.[likee]
    if likers.ContainsKey(liker)
    then
        let struct(ts, count) = likers.[liker]
        likers.[liker] <- struct(ts + (single)likeTs, count+1uy)
    else
        likers.[liker] <- struct((single)likeTs, 1uy)

let handleLikes (likes: Like[]) (account: Account) (deletePrevious: bool) =
    if deletePrevious
    then
        for likeId in account.likes do
            let likers = likesIndex.[likeId]
            likers.Remove(account.id) |> ignore
    for like in likes do
        addLikeToDictionary account.id like.id like.ts

    account.likes <-
        likes
        |> Array.map(fun like -> like.id)
        |> Array.distinct
        |> Array.sortDescending
        |> ResizeArray

let createAccount (accUpd: AccountUpd): Account =
    let account = Account()
    account.id <- accUpd.id.Value
    handleEmail accUpd.email account
    handlePhone accUpd.phone account
    if accUpd.sname |> isNotNull
    then
        handleSecondName accUpd.sname account
    if accUpd.interests |> isNotNull
    then
        handleInterests accUpd.interests account
    account.status <- getStatus accUpd.status
    if (box accUpd.premium) |> isNotNull
    then
        account.premiumStart <- accUpd.premium.start
        account.premiumFinish <- accUpd.premium.finish
    account.premiumNow <- box accUpd.premium |> isNotNull && accUpd.premium.start <= currentTs && accUpd.premium.finish > currentTs
    account.sex <- getSex accUpd.sex
    account.birth <- accUpd.birth.Value
    account.birthYear <- int16 (convertToDate accUpd.birth.Value).Year
    account.joined <- accUpd.joined.Value
    account.joinedYear <- int16 (convertToDate accUpd.joined.Value).Year

    // handle fields with indexes to not fill indexes on failures
    if accUpd.likes |> isNotNull
    then
        handleLikes accUpd.likes account false
    if accUpd.fname |> isNotNull
    then
        handleFirstName accUpd.fname account false
    if accUpd.city |> isNotNull
    then
        handleCity accUpd.city account false
    if accUpd.country |> isNotNull
    then
        handleCountry accUpd.country account false
    updateCityIndex 0uy 0uy 0L [||] 0s 0s account false
    updateCountryIndex 0uy 0uy 0L [||] 0s 0s account false
    updateInterestIndex [||] account false
    emailsDictionary.Add(account.email) |> ignore
    account

let updateExistingAccount (existing: Account, accUpd: AccountUpd) =
    let oldCity = existing.city
    let oldCountry = existing.country
    let oldStatus = existing.status
    let oldSex = existing.sex
    let oldInterests = existing.interests
    let oldJoinedYear = existing.joinedYear
    let oldBirthYear = existing.birthYear

    if accUpd.birth.HasValue
    then
        existing.birth <- accUpd.birth.Value
        existing.birthYear <- int16 (convertToDate accUpd.birth.Value).Year
    if accUpd.joined.HasValue
    then
        existing.joined <- accUpd.joined.Value
        existing.joinedYear <- int16 (convertToDate accUpd.joined.Value).Year
    if accUpd.sex |> isNotNull
    then
        if accUpd.sex.Length > 1
        then raise (ArgumentOutOfRangeException("Sex is wrong"))
        existing.sex <- getSex accUpd.sex
    if accUpd.email |> isNotNull
    then
        handleEmail accUpd.email existing
    if accUpd.phone |> isNotNull
    then
        handlePhone accUpd.phone existing
    if accUpd.sname |> isNotNull
    then
        handleSecondName accUpd.sname existing
    if accUpd.interests |> isNotNull
    then
        handleInterests accUpd.interests existing
    if accUpd.status |> isNotNull
    then
        existing.status <- getStatus accUpd.status
    if box accUpd.premium |> isNotNull
    then
        existing.premiumStart <- accUpd.premium.start
        existing.premiumFinish <- accUpd.premium.finish
        existing.premiumNow <- accUpd.premium.start <= currentTs && accUpd.premium.finish > currentTs

    // handle fields with indexes to not fill indexes on failures
    if accUpd.likes |> isNotNull
    then
        handleLikes accUpd.likes existing true
    if accUpd.fname |> isNotNull
    then
        handleFirstName accUpd.fname existing true
    if accUpd.city |> isNotNull
    then
        handleCity accUpd.city existing true
    if accUpd.country |> isNotNull
    then
        handleCountry accUpd.country existing true
    if accUpd.city |> isNotNull || accUpd.sex |> isNotNull || accUpd.status |> isNotNull || accUpd.interests |> isNotNull || accUpd.joined.HasValue || accUpd.birth.HasValue
    then
        updateCityIndex oldSex oldStatus oldCity oldInterests oldBirthYear oldJoinedYear existing true
    if accUpd.country |> isNotNull || accUpd.sex |> isNotNull || accUpd.status |> isNotNull || accUpd.interests |> isNotNull || accUpd.joined.HasValue || accUpd.birth.HasValue
    then
        updateCountryIndex oldSex oldStatus oldCountry oldInterests oldBirthYear oldJoinedYear existing true
    if accUpd.interests |> isNotNull
    then
        updateInterestIndex oldInterests existing true
    if accUpd.email |> isNotNull
    then
        emailsDictionary.Add(accUpd.email) |> ignore
    ()

let newAccount (next, ctx : HttpContext) =
    Interlocked.Increment(newAccountCount) |> ignore
    task {
        try
            let! json = ctx.ReadBodyFromRequestAsync()
            let account = deserializeObjectUnsafe<AccountUpd>(json)
            if account.id.HasValue |> not
            then
                return! setStatusCode 400 next ctx
            else
                accounts.[account.id.Value] <- createAccount account
                Interlocked.Increment(&accountsNumber) |> ignore
                return! writePostResponse 201 next ctx
        with
        | :? ArgumentOutOfRangeException ->
            return! setStatusCode 400 next ctx
        | :? JsonParsingException ->
            return! setStatusCode 400 next ctx
    }

let updateAccount (id, next, ctx : HttpContext) =
    Interlocked.Increment(updateAccountCount) |> ignore
    if (box accounts.[id] |> isNull)
    then
        setStatusCode 404 next ctx
    else
        task {
            try
                let! json = ctx.ReadBodyFromRequestAsync()
                let account = deserializeObjectUnsafe<AccountUpd>(json)
                let target = accounts.[id]
                let rollback = target.CreateCopy()
                try
                    updateExistingAccount(target, account)
                    return! writePostResponse 202 next ctx
                with
                | :? ArgumentOutOfRangeException ->
                    accounts.[id] <- rollback
                    return! setStatusCode 400 next ctx

                | :? KeyNotFoundException ->
                    return! setStatusCode 400 next ctx
            with
            | :? JsonParsingException ->
                return! setStatusCode 400 next ctx
        }