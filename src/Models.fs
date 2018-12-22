namespace HCup.Models

open System
open System.Collections.Concurrent


type Location() =
        [<DefaultValue>]val mutable id: int32
        [<DefaultValue>]val mutable distance : uint8
        [<DefaultValue>]val mutable city: byte[]
        [<DefaultValue>]val mutable place: byte[]
        [<DefaultValue>]val mutable country: string

type LocationOld() =
        [<DefaultValue>]val mutable id: int32
        [<DefaultValue>]val mutable distance : uint8
        [<DefaultValue>]val mutable city: string
        [<DefaultValue>]val mutable place: string
        [<DefaultValue>]val mutable country: string



[<CLIMutable>]
type LocationUpd =
    {
        distance : Nullable<uint8>
        city: string
        place: string
        country: string
    }

[<CLIMutable>]
type Locations =
    {
        locations : LocationOld[]
    }

[<CLIMutable>]
type UserUpd =
    {
        first_name : string
        last_name: string
        birth_date: Nullable<int32>
        gender: Nullable<char>
        email: string
    }

type User() =
        [<DefaultValue>]val mutable id: int32
        [<DefaultValue>]val mutable first_name : byte[]
        [<DefaultValue>]val mutable last_name: byte[]
        [<DefaultValue>]val mutable birth_date: int32
        [<DefaultValue>]val mutable gender: char
        [<DefaultValue>]val mutable email: byte[]

type Premium() =
        [<DefaultValue>]val mutable start: int32
        [<DefaultValue>]val mutable finish : int32

type Like() =
        [<DefaultValue>]val mutable ts: int32
        [<DefaultValue>]val mutable id : int32

type AccountField =
    | Firstname = 0
    | Surname = 1
    | Email = 2
    | Interests = 3
    | Status = 4
    | Premium = 5
    | Sex = 6
    | Phone = 7
    | Likes = 8
    | Birth = 9
    | City = 10
    | Country = 11


type AccountUpd() =
        [<DefaultValue>]val mutable id: int32
        [<DefaultValue>]val mutable fname : string
        [<DefaultValue>]val mutable sname : string
        [<DefaultValue>]val mutable email: string
        [<DefaultValue>]val mutable interests: string[]
        [<DefaultValue>]val mutable status: string
        [<DefaultValue>]val mutable premium: Premium
        [<DefaultValue>]val mutable sex: char
        [<DefaultValue>]val mutable phone: string
        [<DefaultValue>]val mutable likes: Like[]
        [<DefaultValue>]val mutable birth: int32
        [<DefaultValue>]val mutable joined: int32
        [<DefaultValue>]val mutable city: string
        [<DefaultValue>]val mutable country: string

type Account() =
        [<DefaultValue>]val mutable id: int32
        [<DefaultValue>]val mutable fname : string
        [<DefaultValue>]val mutable sname : string
        [<DefaultValue>]val mutable email: string
        [<DefaultValue>]val mutable emailDomain: string
        [<DefaultValue>]val mutable interests: string[]
        [<DefaultValue>]val mutable status: int32
        [<DefaultValue>]val mutable premium: Premium
        [<DefaultValue>]val mutable premiumNow: bool
        [<DefaultValue>]val mutable sex: char
        [<DefaultValue>]val mutable phone: string
        [<DefaultValue>]val mutable phoneCode: int32
        [<DefaultValue>]val mutable likes: Like[]
        [<DefaultValue>]val mutable birth: int32
        [<DefaultValue>]val mutable birthYear: int32
        [<DefaultValue>]val mutable joined: int32
        [<DefaultValue>]val mutable joinedYear: int32
        [<DefaultValue>]val mutable city: string
        [<DefaultValue>]val mutable country: string


[<CLIMutable>]
type AccountsUpd =
    {
        accounts : AccountUpd[]
    }

[<CLIMutable>]
type VisitUpd =
    {
        user : Nullable<int32>
        location: Nullable<int32>
        visited_at: Nullable<uint32>
        mark: Nullable<uint8>
    }

type Visit() =
        [<DefaultValue>]val mutable id: int32
        [<DefaultValue>]val mutable user : int32
        [<DefaultValue>]val mutable location: int32
        [<DefaultValue>]val mutable visited_at: uint32
        [<DefaultValue>]val mutable mark: uint8

[<CLIMutable>]
type Visits =
    {
        visits : Visit[]
    }

[<Struct>]
type StructOption<'a> =
    | Som of 'a
    | Non

[<Struct>]
type UserVisit = { mark: uint8; visited_at: uint32; place: byte[] }