module HCup.Filters

open System
open HCup.Models
open HCup.Helpers
open System.Collections.Generic
open Giraffe

type Filter = string -> Account -> bool

let inline getStatus status =
    match status with
    | "свободны" -> freeStatus
    | "всё сложно" -> complexStatus
    | "заняты" -> occupiedStatus
    | _ -> failwith "Invalid status"

let sexEqFilter (value: string) =
    fun (acc: Account) -> acc.sex = value.[0]

let emailDomainFilter (value: string) =
    fun (acc: Account) ->
        if acc.email |> isNull
        then
            false
        else
            let atIndex = acc.email.IndexOf('@', StringComparison.Ordinal)
            acc.email.Substring(atIndex+1) =~ value

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
        acc.fname =~ value

let firstNameAnyFilter (value: string) =
    fun (acc: Account) ->
        value.Split(',') |> Array.exists (fun el -> el =~ acc.fname)

let firstNameNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.fname |> isNull
        else acc.fname |> isNotNull

let surnameEqFilter (value: string) =
    fun (acc: Account) ->
        acc.sname =~ value

let surnameStartsFilter (value: string) =
    fun (acc: Account) ->
        if acc.sname |> isNull
        then
            false
        else
            acc.sname.StartsWith(value)

let surnameNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.sname |> isNull
        else acc.sname |> isNotNull

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
        acc.country =~ value

let countryNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.country |> isNull
        else acc.country |> isNotNull

let cityEqFilter (value: string) =
    fun (acc: Account) ->
        acc.city =~ value

let cityAnyFilter (value: string) =
    fun (acc: Account) ->
        value.Split(',') |> Array.exists (fun el -> el =~ acc.city)

let cityNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then acc.city |> isNull
        else acc.city |> isNotNull

let birthLtFilter (value: string) =
    fun (acc: Account) ->
        acc.birth < Int32.Parse(value)

let birthGtFilter (value: string) =
    fun (acc: Account) ->
        acc.birth > Int32.Parse(value)

let birthYearFilter (value: string) =
    fun (acc: Account) ->
        acc.birthYear = Int32.Parse(value)

let joinedYearFilter (value: string) =
    fun (acc: Account) ->
        acc.joinedYear = Int32.Parse(value)

let interestsContainsFilter (value: string) =
    fun (acc: Account) ->
        acc.interests |> isNotNull
            && value.Split(',') |> Array.forall (fun interest -> acc.interests |> Array.exists (fun el -> el =~ interest))

let interestsAnyFilter (value: string) =
    fun (acc: Account) ->
        acc.interests |> isNotNull
            && value.Split(',') |> Array.exists (fun interest -> acc.interests |> Array.exists (fun el -> el =~ interest))

let likesContainsFilter (value: string) =
    fun (acc: Account) ->
        acc.likes |> isNotNull
            && value.Split(',') |> Array.forall (fun id -> acc.likes |> Array.exists (fun el -> el.id = Int32.Parse(id)))

let premiumNowFilter (value: string) =
    fun (acc: Account) ->
        acc.premiumNow

let premiumNullFilter (value: string) =
    fun (acc: Account) ->
        if value.[0] = '1'
        then box acc.premium |> isNull
        else box acc.premium |> isNotNull

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

let groupFilters: IDictionary<string, Filter> =
    dict [
        "sex", sexEqFilter
        "status", statusEqFilter
        "fname", firstNameEqFilter
        "sname", surnameEqFilter
        "country", countryEqFilter
        "city", cityEqFilter
        "birth", birthYearFilter
        "interests", interestsContainsFilter
        "likes", likesContainsFilter
        "joined", joinedYearFilter
    ]
