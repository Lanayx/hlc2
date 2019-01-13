module HCup.Filters

open System
open HCup.Models
open HCup.Common
open HCup.Dictionaries
open System.Collections.Generic
open Giraffe
open BitmapIndex

type Filter = string -> Account -> bool

let inline getStatus status =
    match status with
    | "свободны" -> freeStatus
    | "всё сложно" -> complexStatus
    | "заняты" -> occupiedStatus
    | _ -> raise (ArgumentOutOfRangeException("Status is invalid"))

let inline getSex status =
    match status with
    | "f" -> female
    | "m" -> male
    | _ -> raise (ArgumentOutOfRangeException("Sex is invalid"))

let sexEqFilter (value: string) =
    fun (acc: Account) ->
        let sex = getSex value
        acc.sex = sex

let emailDomainFilter (value: string) =
    fun (acc: Account) ->
        if acc.emailDomain |> isNull
        then
            false
        else
            acc.emailDomain =~ value

let emailLtFilter (value: string) =
    fun (acc: Account) ->
        acc.email < value

let emailGtFilter (value: string) =
    fun (acc: Account) ->
        acc.email > value

let statusEqFilter (value: string) =
    fun (acc: Account) ->
        let intStatus = getStatus value
        acc.status = intStatus

let statusNeqFilter (value: string) =
    fun (acc: Account) ->
        let intStatus = getStatus value
        acc.status <> intStatus

let firstNameEqFilter (value: string) =
    fun (acc: Account) ->
        let mutable name = 0uy
        fnamesWeightDictionary.TryGetValue(value, &name) && acc.fname = name

let firstNameAnyFilter (value: string) =
    fun (acc: Account) ->
        value.Split(',') |> Array.exists (fun el -> fnamesWeightDictionary.[el] = acc.fname)

let firstNameNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.fname = 0uy
        else acc.fname <> 0uy

let surnameEqFilter (value: string) =
    fun (acc: Account) ->
        if acc.sname = 0s
        then
            false
        else
            let mutable sname = 0s
            snamesWeightDictionary.TryGetValue(value, &sname) && acc.sname = sname

let surnameStartsFilter (value: string) =
    fun (acc: Account) ->
        if acc.sname = 0s
        then
            false
        else
            let strSname = snamesWeightDictionaryReverse.[acc.sname]
            strSname.StartsWith(value,StringComparison.Ordinal)

let surnameNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.sname = 0s
        else acc.sname <> 0s

let phoneCodeFilter (value: string) =
    fun (acc: Account) ->
        if acc.phone |> isNull
        then
            false
        else
            let intCode = Int32.Parse(value)
            acc.phoneCode = intCode

let phoneNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.phone |> isNull
        else acc.phone |> isNotNull

let countryEqFilter (value: string) =
    fun (acc: Account) ->
        let mutable country = 0uy
        countriesWeightDictionary.TryGetValue(value, &country) && acc.country = country

let countryNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.country = 0uy
        else acc.country <> 0uy

let cityEqFilter (value: string) =
    fun (acc: Account) ->
        let mutable city = 0s
        citiesWeightDictionary.TryGetValue(value, &city) && acc.city = city

let cityAnyFilter (value: string) =
    fun (acc: Account) ->
        value.Split(',') |> Array.exists (fun el ->
            let mutable cityWeight = 0s
            citiesWeightDictionary.TryGetValue(el, &cityWeight) && cityWeight = acc.city
        )

let cityNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.city = 0s
        else acc.city <> 0s

let birthLtFilter (value: string) =
    fun (acc: Account) ->
        acc.birth < Int32.Parse(value)

let birthGtFilter (value: string) =
    fun (acc: Account) ->
        acc.birth > Int32.Parse(value)

let birthYearFilter (value: string) =
    fun (acc: Account) ->
        acc.birthYear = Int16.Parse(value)

let joinedYearFilter (value: string) =
    fun (acc: Account) ->
        acc.joinedYear = Int16.Parse(value)

let interestsContainsFilter (value: string) =
    fun (acc: Account) ->
        acc.interests |> isNotNull
            && value.Split(',')
            |> Seq.map(fun el -> interestsWeightDictionary.[el])
            |> Seq.forall (fun interest -> acc.interests |> Array.exists (fun el -> el = interest))

let interestsContainsOneFilter (value: string) =
    fun (acc: Account) ->
        acc.interests |> isNotNull
            && acc.interests |> Array.contains interestsWeightDictionary.[value]

let interestsAnyFilter (value: string) =
    fun (acc: Account) ->
        acc.interests |> isNotNull
            && value.Split(',')
            |> Seq.map(fun el -> interestsWeightDictionary.[el])
            |> Seq.exists (fun interest -> acc.interests |> Array.exists (fun el -> el = interest))

let likesContainsFilter (value: string) =
    fun (acc: Account) ->
        acc.likes |> isNotNull
            && value.Split(',') |> Array.forall (fun id -> acc.likes.Contains(Int32.Parse(id)))

let likesContainsOneFilter (value: string) =
    fun (acc: Account) ->
        acc.likes |> isNotNull
            && acc.likes.Contains(Int32.Parse(value))

let premiumNowFilter (value: string) =
    fun (acc: Account) ->
        acc.premiumNow

let premiumNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.premiumStart = 0
        else acc.premiumStart <> 0

let filtersOrder: IDictionary<string, int> =
    dict [
        "sex_eq", 0
        "email_domain", 0
        "email_lt", 0
        "email_gt", 0
        "status_eq", 0
        "status_neq", 0
        "fname_eq", 0
        "fname_any", 1
        "fname_null", 0
        "sname_eq", 0
        "sname_starts", 0
        "sname_null", 0
        "phone_code", 0
        "phone_null", 0
        "country_eq", 0
        "country_null", 0
        "city_eq", 0
        "city_any", 0
        "city_null", 0
        "birth_lt", 0
        "birth_gt", 0
        "birth_year", 0
        "interests_contains", 2
        "interests_any", 2
        "likes_contains", 0
        "premium_now", 0
        "premium_null", 0
    ]

let filters: IDictionary<string, Filter> =
    dict [
        "sex_eq", sexEqFilter
        "email_domain", emailDomainFilter
        "email_lt", emailLtFilter
        "email_gt", emailGtFilter
        "status_eq", statusEqFilter
        "status_neq", statusNeqFilter
        "fname_eq", firstNameEqFilter
        "fname_any", firstNameAnyFilter
        "fname_null", firstNameNullFilter
        "sname_eq", surnameEqFilter
        "sname_starts", surnameStartsFilter
        "sname_null", surnameNullFilter
        "phone_code", phoneCodeFilter
        "phone_null", phoneNullFilter
        "country_eq", countryEqFilter
        "country_null", countryNullFilter
        "city_eq", cityEqFilter
        "city_any", cityAnyFilter
        "city_null", cityNullFilter
        "birth_lt", birthLtFilter
        "birth_gt", birthGtFilter
        "birth_year", birthYearFilter
        "interests_contains", interestsContainsFilter
        "interests_any", interestsAnyFilter
        "likes_contains", likesContainsFilter
        "premium_now", premiumNowFilter
        "premium_null", premiumNullFilter
    ]

let groupFiltersOrder: IDictionary<string, int> =
    dict [
        "sex", 0
        "status", 0
        "fname", 0
        "sname", 0
        "country", 0
        "city", 0
        "birth", 0
        "interests", 1
        "likes", 0
        "joined", 0
    ]

let groupFilters: IDictionary<string, Filter> =
    dict [
        "sex", sexEqFilter
        "status", statusEqFilter
        "fname", firstNameEqFilter
        "sname", surnameEqFilter
        "country", countryEqFilter
        "city", cityEqFilter
        "birth", birthYearFilter
        "interests", interestsContainsOneFilter
        "likes", likesContainsOneFilter
        "joined", joinedYearFilter
    ]

let recommendFilters: IDictionary<string, Filter> =
    dict [
        "country", countryEqFilter
        "city", cityEqFilter
    ]
