# `GroupBy` Performance
Looking at the performance of `GroupBy` in various scenarios

We want to see how `GroupBy` performs in various scenarios, let's test!
This project is all .NET Core 3.1, previous editions may differ due to newer
.NET Core LINQ optimizations.

We will test two scenarios, each using a toy `Bill` class standing in for a typical thing one might group on.
Scenario one will have 10,000 keys each with 10 bills, scenario two will have 10 keys each with 10,000 bills.



## Aggregating by group
One thing you might use group by for is to compute some aggregate value by grouping. For instance with our bill, we might want to know the sum of the totals for each by, grouped by bill type. `Group By` provides a very concise way to do this:

```c#
return bills.GroupBy(b => b.BillType).Select(grouping => grouping.Sum(b => b.Total)).ToList();
```
This will return a List of the sums of all the totals or each group type.

Avoiding group by, another way we could compute the sums is like so:
```c#
 var sums = new Dictionary<int, double>();
 foreach (var bill in bills)
 {
     double total;
     if (!sums.TryGetValue(bill.BillType, out total))
     {
         sums.Add(bill.BillType, bill.Total);
     }
     else
     {                    
         sums[bill.BillType] = total + bill.Total;
     }
 }
 return sums.Values.ToList();
```
This is a lot more code, and we might expect this to perform worse, since we are creating a `Dictionary` to hold a bunch of intermediate data before we aggregate. But it turns out, so is `GroupBy`, let's look at a BenchmarkDotNet comparison:

|             Method | GroupCount |      Mean |     Error |    StdDev |    Gen 0 |    Gen 1 |    Gen 2 |  Allocated |
|------------------- |----------- |----------:|----------:|----------:|---------:|---------:|---------:|-----------:|
|       GroupBy_Sums |         10 |  3.323 ms | 0.0200 ms | 0.0187 ms | 367.1875 | 292.9688 | 210.9375 | 2566.69 KB |
|    Dictionary_Sums |         10 |  1.553 ms | 0.0054 ms | 0.0048 ms |        - |        - |        - |    1.16 KB |
|       GroupBy_Sums |      10000 | 12.396 ms | 0.1093 ms | 0.1022 ms | 578.1250 | 296.8750 |  78.1250 | 4586.58 KB |
|    Dictionary_Sums |      10000 |  2.181 ms | 0.0178 ms | 0.0158 ms | 121.0938 | 101.5625 |  85.9375 |  998.24 KB |

Doing the aggregation as you go saves a ton of time and GC pressure. This is because `GroupBy` can't perform this operation in a totally lazy manner, it is having to creating a lookup table behind the scenes, one that is filled with a collection of Bills, rather than just the current sum.

## Building up a cache for later use

Another scenario we often use `GroupBy` for is to create caches/lookup tables for use later in our application.  For instance a website may pull down data from a database on startup, and create an in memory cache to avoid hammering the database for frequently queries but rarely changed data. With this use case, there are two concerns. How long it takes to create the cache, and then how well the cache performs when using it. We will take a look at both. In our case we want some kind of lookup table that lets us get a collection of `Bills` when we provide the `BillType`. We could do that by using GroupBy, ToLookup, or by creating a Dictionary by hand:
``` c#
 
public Dictionary<int, IEnumerable<Bill>> GroupBy_Cache()
{
    return bills.GroupBy(b => b.BillType).ToDictionary(g => g.Key, g => (IEnumerable<Bill>)g);
}
 
public ILookup<int,Bill> Lookup_Cache()
{
    return bills.ToLookup(b => b.BillType);
}

public Dictionary<int,List<Bill>> Dictionary_Cache()
{
    var dict = new Dictionary<int, List<Bill>>();
    foreach (var bill in bills)
    {
        List<Bill> billList;
        if (!dict.TryGetValue(bill.BillType, out billList))
        {
            billList = new List<Bill>();
            dict.Add(bill.BillType, billList);
        }
        billList.Add(bill);

    }
    return dict;
}
```






