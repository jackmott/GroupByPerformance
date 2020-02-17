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
