module HCup.Dictionaries

open System.Collections.Generic
open HCup.Models
open System

type FnameGroup = byte
type SnameGroup = int16
type BirthGroup = int16
type JoinedGroup = int16
type SexGroup = byte
type StatusGroup = byte
type CityGroup = int16
type CountryGroup = byte
type InterestsGroup = byte
type CountType = int
type FourthField = (struct(CountType*Dictionary<JoinedGroup,CountType>*Dictionary<BirthGroup,CountType>))


let fnamesWeightDictionary = Dictionary<string, byte>()
let snamesWeightDictionary = Dictionary<string, int16>()
let mutable snamesWeightDictionaryReverse = Dictionary<int16, string>()
let citiesWeightDictionary = Dictionary<string, int16>()
let countriesWeightDictionary = Dictionary<string, byte>()
let interestsWeightDictionary = Dictionary<string, byte>()

let mutable fnamesSerializeDictionary : byte[][] = null
let mutable snamesSerializeDictionary : byte[][] = null
let mutable citiesSerializeDictionary : byte[][] = null
let mutable countriesSerializeDictionary : byte[][] = null
let mutable interestsSerializeDictionary : byte[][] = null

// array by likees, value is dict of key=userId, value=sumOfTs*tsCount
let likesIndex: SortedSet<int>[] = Array.zeroCreate(1400000)
// key is cityWeight, value is usersId's set
let citiesIndex = Dictionary<CityGroup, SortedSet<int>>()
let countriesIndex = Dictionary<CountryGroup, SortedSet<int>>()
let fnamesIndex = Dictionary<FnameGroup, SortedSet<int>>()
let emailsDictionary = HashSet<string>()

// GROUPING

let allCountryGroups =
    Dictionary<CountryGroup,struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,FourthField>*Dictionary<StatusGroup,FourthField>)>()
let allCityGroups =
    Dictionary<CityGroup,struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,FourthField>*Dictionary<StatusGroup,FourthField>)>()
let allSexGroups =
    Dictionary<SexGroup,struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,FourthField>*Dictionary<CountryGroup,FourthField>*Dictionary<StatusGroup,FourthField>)>()
let allStatusGroups =
    Dictionary<StatusGroup,struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,FourthField>*Dictionary<CountryGroup,FourthField>*Dictionary<SexGroup,FourthField>)>()
let allInterestsGroups =
    Dictionary<InterestsGroup,struct(Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<CityGroup,FourthField>*Dictionary<CountryGroup,FourthField>*Dictionary<SexGroup,FourthField>*Dictionary<StatusGroup,FourthField>)>()
let allCountrySexGroups =
    Dictionary<struct(CountryGroup*SexGroup),struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<StatusGroup,FourthField>)>()
let allCitySexGroups =
    Dictionary<struct(CityGroup*SexGroup),struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<StatusGroup,FourthField>)>()
let allCountryStatusGroups =
    Dictionary<struct(CountryGroup*StatusGroup),struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,FourthField>)>()
let allCityStatusGroups =
    Dictionary<struct(CityGroup*StatusGroup),struct(Dictionary<InterestsGroup,FourthField>*Dictionary<BirthGroup,CountType>*Dictionary<JoinedGroup,CountType>*Dictionary<SexGroup,FourthField>)>()

// RECOMMEND

let mutable bestMaleUsersCity = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestMaleUsersCountry = Dictionary<CountryGroup, ResizeArray<Account>>()
let mutable bestMaleUsers2City = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestMaleUsers2Country = Dictionary<CountryGroup, ResizeArray<Account>>()
let mutable bestMaleUsers3City = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestMaleUsers3Country = Dictionary<CountryGroup, ResizeArray<Account>>()

let mutable bestSimpleMaleUsersCity = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestSimpleMaleUsersCountry = Dictionary<CountryGroup, ResizeArray<Account>>()
let mutable bestSimpleMaleUsers2City = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestSimpleMaleUsers2Country = Dictionary<CountryGroup, ResizeArray<Account>>()
let mutable bestSimpleMaleUsers3City = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestSimpleMaleUsers3Country = Dictionary<CountryGroup, ResizeArray<Account>>()

let mutable bestFemaleUsersCity = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestFemaleUsersCountry = Dictionary<CountryGroup, ResizeArray<Account>>()
let mutable bestFemaleUsers2City = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestFemaleUsers2Country = Dictionary<CountryGroup, ResizeArray<Account>>()
let mutable bestFemaleUsers3City = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestFemaleUsers3Country = Dictionary<CountryGroup, ResizeArray<Account>>()

let mutable bestSimpleFemaleUsersCity = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestSimpleFemaleUsersCountry = Dictionary<CountryGroup, ResizeArray<Account>>()
let mutable bestSimpleFemaleUsers2City = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestSimpleFemaleUsers2Country = Dictionary<CountryGroup, ResizeArray<Account>>()
let mutable bestSimpleFemaleUsers3City = Dictionary<CityGroup, ResizeArray<Account>>()
let mutable bestSimpleFemaleUsers3Country = Dictionary<CountryGroup, ResizeArray<Account>>()