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

type LikeFull() =
        [<DefaultValue>]val mutable ts: int32
        [<DefaultValue>]val mutable liker : int32
        [<DefaultValue>]val mutable likee : int32

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
        [<DefaultValue>]val mutable id: Nullable<int32>
        [<DefaultValue>]val mutable fname : string
        [<DefaultValue>]val mutable sname : string
        [<DefaultValue>]val mutable email: string
        [<DefaultValue>]val mutable interests: string[]
        [<DefaultValue>]val mutable status: string
        [<DefaultValue>]val mutable premium: Premium
        [<DefaultValue>]val mutable sex: string
        [<DefaultValue>]val mutable phone: string
        [<DefaultValue>]val mutable likes: Like[]
        [<DefaultValue>]val mutable birth: Nullable<int32>
        [<DefaultValue>]val mutable joined: Nullable<int32>
        [<DefaultValue>]val mutable city: string
        [<DefaultValue>]val mutable country: string

[<Struct>]
type SmartLike =
    {
        likee: int
        sumOfTs: single
    }

type Account() =
        [<DefaultValue>]val mutable id: int
        [<DefaultValue>]val mutable fname : byte
        [<DefaultValue>]val mutable sname : int16
        [<DefaultValue>]val mutable email: string
        [<DefaultValue>]val mutable emailDomain: string
        [<DefaultValue>]val mutable interests: byte[]
        [<DefaultValue>]val mutable status: byte
        [<DefaultValue>]val mutable premiumStart: int
        [<DefaultValue>]val mutable premiumFinish: int
        [<DefaultValue>]val mutable premiumNow: bool
        [<DefaultValue>]val mutable sex: byte
        [<DefaultValue>]val mutable phone: string
        [<DefaultValue>]val mutable phoneCode: int
        [<DefaultValue>]val mutable birth: int
        [<DefaultValue>]val mutable birthYear: int16
        [<DefaultValue>]val mutable likes: ResizeArray<SmartLike>
        [<DefaultValue>]val mutable joined: int
        [<DefaultValue>]val mutable joinedYear: int16
        [<DefaultValue>]val mutable city: int16
        [<DefaultValue>]val mutable country: byte

        member this.CreateCopy() =
            let copy = Account()
            copy.id <- this.id
            copy.fname <- this.fname
            copy.sname <- this.sname
            copy.email <- this.email
            copy.emailDomain <- this.emailDomain
            copy.interests <- this.interests
            copy.status <- this.status
            copy.premiumStart <- this.premiumStart
            copy.premiumFinish <- this.premiumFinish
            copy.premiumNow <- this.premiumNow
            copy.sex <- this.sex
            copy.phone <- this.phone
            copy.phoneCode <- this.phoneCode
            copy.birth <- this.birth
            copy.birthYear <- this.birthYear
            copy.likes <- this.likes
            copy.joined <- this.joined
            copy.joinedYear <- this.joinedYear
            copy.city <- this.city
            copy.country <- this.country
            copy


[<CLIMutable>]
type AccountsUpd =
    {
        accounts : AccountUpd[]
    }

[<CLIMutable>]
type LikesUpd =
    {
        likes : LikeFull[]
    }