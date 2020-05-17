using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

//
// Crontab expression format:
//
// * * * * *
// - - - - -
// | | | | |
// | | | | +----- day of week (0 - 6) (Sunday=0)
// | | | +------- month (1 - 12)
// | | +--------- day of month (1 - 31)
// | +----------- hour (0 - 23)
// +------------- min (0 - 59)
//
// Star (*) in the value field above means all legal values as in 
// braces for that column. The value column can have a * or a list 
// of elements separated by commas. An element is either a number in 
// the ranges shown above or two numbers in the range separated by a 
// hyphen (meaning an inclusive range). 
//
// Source: http://www.adminschoice.com/docs/crontab.htm
//

// Six-part expression format:
//
// * * * * * *
// - - - - - -
// | | | | | |
// | | | | | +--- day of week (0 - 6) (Sunday=0)
// | | | | +----- month (1 - 12)
// | | | +------- day of month (1 - 31)
// | | +--------- hour (0 - 23)
// | +----------- min (0 - 59)
// +------------- sec (0 - 59)
//
// The six-part expression behaves similarly to the traditional 
// crontab format except that it can denotate more precise schedules 
// that use a seconds component.
// 

namespace croncon
{
    public static class CronCalc
    {
        public static DateTime NextFire(string expression, DateTime? now = null, DateTime? end = null)
        {
            var schedule = Parse(expression);
            return GetNextOccurrence(schedule, now??DateTime.Now, end??DateTime.MaxValue);
        }

        private static Dictionary<Kind, Field> Parse(string expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            var tokens = expression.Split(Separator.Space, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length > 6 || tokens.Length < 5)
                throw new ArgumentOutOfRangeException(
                    $"{nameof(expression)} must contain 5 or 6 tokens (actual {tokens.Length} : {expression})"
                );

            var result = new Dictionary<Kind, Field>(tokens.Length);

            var i = 0;
            foreach (var token in tokens.Reverse())
            {
                var kind = (Kind) i++;
                result[kind] = FieldProducer[kind](token);
            }

            if (!result.ContainsKey(Kind.Seconds))
                result[Kind.Seconds] = ZeroSeconds;

            return result;
        }

        private static DateTime GetNextOccurrence(IDictionary<Kind, Field> schedule, DateTime baseTime, DateTime endTime)
        {
            while (true)
            {
                var baseYear   = baseTime.Year;
                var baseMonth  = baseTime.Month;
                var baseDay    = baseTime.Day;
                var baseHour   = baseTime.Hour;
                var baseMinute = baseTime.Minute;
                var baseSecond = baseTime.Second;

                var endYear  = endTime.Year;
                var endMonth = endTime.Month;
                var endDay   = endTime.Day;

                var year   = baseYear;
                var month  = baseMonth;
                var day    = baseDay;
                var hour   = baseHour;
                var minute = baseMinute;
                var second = baseSecond + 1;

                second = schedule[Kind.Seconds].Next(second);

                if (second == Nil)
                {
                    second = schedule[Kind.Seconds].GetFirst();
                    ++minute;
                }

                minute = schedule[Kind.Minutes].Next(minute);

                if (minute == Nil)
                {
                    minute = schedule[Kind.Minutes].GetFirst();
                    ++hour;
                }

                hour = schedule[Kind.Hours].Next(hour);

                if (hour == Nil)
                {
                    minute = schedule[Kind.Minutes].GetFirst();
                    hour   = schedule[Kind.Hours].GetFirst();
                    ++day;
                }
                else if (hour > baseHour)
                {
                    minute = schedule[Kind.Minutes].GetFirst();
                }

                day = schedule[Kind.Days].Next(day);

                while (true)
                {
                    if (day == Nil)
                    {
                        second = schedule[Kind.Seconds].GetFirst();
                        minute = schedule[Kind.Minutes].GetFirst();
                        hour   = schedule[Kind.Hours].GetFirst();
                        day    = schedule[Kind.Days].GetFirst();
                        ++month;
                    }
                    else if (day > baseDay)
                    {
                        second = schedule[Kind.Seconds].GetFirst();
                        minute = schedule[Kind.Minutes].GetFirst();
                        hour   = schedule[Kind.Hours].GetFirst();
                    }

                    month = schedule[Kind.Months].Next(month);

                    if (month == Nil)
                    {
                        second = schedule[Kind.Seconds].GetFirst();
                        minute = schedule[Kind.Minutes].GetFirst();
                        hour   = schedule[Kind.Hours].GetFirst();
                        day    = schedule[Kind.Days].GetFirst();
                        month  = schedule[Kind.Months].GetFirst();
                        year++;
                    }
                    else if (month > baseMonth)
                    {
                        second = schedule[Kind.Seconds].GetFirst();
                        minute = schedule[Kind.Minutes].GetFirst();
                        hour   = schedule[Kind.Hours].GetFirst();
                        day    = schedule[Kind.Days].GetFirst();
                    }

                    var dateChanged = day != baseDay || month != baseMonth || year != baseYear;

                    if (day > 28 && dateChanged && day > DateTime.DaysInMonth(year, month))
                    {
                        if (year >= endYear && month >= endMonth && day >= endDay) return endTime;

                        day = Nil;
                    }
                    else
                        break;
                }

                var nextTime = new DateTime(year, month, day, hour, minute, second, 0, baseTime.Kind);

                if (nextTime >= endTime)
                    return endTime;

                if (schedule[Kind.DaysOfWeek].Contains((int) nextTime.DayOfWeek)) 
                    return nextTime;

                baseTime = new DateTime(year, month, day, 23, 59, 59, 0, baseTime.Kind);
            }
        }

        #region consts

        private const int Nil = -1;

        private static class Separator
        {
            public const string Space = " ";
            public const string Comma = ",";
            public const string Slash = "/";
            public const string Dash  = "-";
        }

        private static readonly string[] DaysOfWeek =
        {
            "Sunday",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday"
        };

        private static readonly string[] Months =
        {
            "January",
            "February",
            "March",
            "April",
            "May",
            "June",
            "July",
            "August",
            "September",
            "October",
            "November",
            "December"
        };
        
        private enum Kind
        {
            DaysOfWeek = 0,
            Months,
            Days,
            Hours,
            Minutes,
            Seconds
        }

        #endregion
            
        private static readonly Dictionary<Kind, Func<string, Field>> FieldProducer =
            new Dictionary<Kind, Func<string, Field>>
            {
                {Kind.DaysOfWeek, x => new Field(x, 0,  6, DaysOfWeek)},
                {Kind.Months,     x => new Field(x, 1, 12, Months)},
                {Kind.Days,       x => new Field(x, 1, 31)},
                {Kind.Hours,      x => new Field(x, 0, 23)},
                {Kind.Minutes,    x => new Field(x, 0, 59)},
                {Kind.Seconds,    x => new Field(x, 0, 59)}
            };

        private static readonly Field ZeroSeconds = new Field("0", 0, 59); 

        private class Field
        {
            private readonly string[] names;
            private readonly int      maxValue;
            private readonly int      minValue;

            private readonly BitArray bits;
            private          int      minValueSet;
            private          int      maxValueSet;

            public Field(string token, int minValue, int maxValue, string[] names = null)
            {
                this.minValue = minValue;
                this.maxValue = maxValue;
                this.names    = names;
                
                bits = new BitArray(maxValue - minValue + 1);
                minValueSet = minValue;
                maxValueSet = maxValue;
                
                try
                {
                    ParseToken(token);
                }
                catch (Exception e)
                {
                    throw new AggregateException($"Can't parse token \"{token}\"", e);
                }
            }

            private void ParseToken(string token)
            {
                if (string.IsNullOrWhiteSpace(token))
                    throw new ArgumentNullException(nameof(token));

                if (token.Contains(Separator.Comma))
                {
                    using var subtoken = ((IEnumerable<string>) token.Split(Separator.Comma)).GetEnumerator();
                    while (subtoken.MoveNext())
                        ParseToken(subtoken.Current);
                    return;
                }

                var every      = 1;
                var slashIndex = token.IndexOf(Separator.Slash, StringComparison.Ordinal);
                if (slashIndex > 0)
                {
                    every = int.Parse(token.Substring(slashIndex + 1), CultureInfo.InvariantCulture);
                    token = token.Substring(0, slashIndex);
                }
                
                if (token.Length == 1 && token[0] == '*')
                {
                    Accumulate(-1, -1, every);
                    return;
                }
                
                var dashIndex = token.IndexOf(Separator.Dash, StringComparison.Ordinal);
        
                if (dashIndex > 0)
                {
                    var first = ParseValue(token.Substring(0, dashIndex));
                    var last  = ParseValue(token.Substring(dashIndex + 1));

                    Accumulate(first, last, every);
                    return;
                }
                
                var value = ParseValue(token);

                if (every == 1)
                {
                    Accumulate(value, value, 1);
                    return;
                }

                if(every != 0)
                    throw new ArgumentException($"\"{token}\" has not valid (zero) interval");
                Accumulate(value, maxValue, every);
            }
            
            private int ParseValue(string subtoken)
            {
                if (string.IsNullOrWhiteSpace(subtoken))
                    throw new ArgumentNullException(nameof(subtoken));

                var firstChar = subtoken[0];
        
                if (firstChar >= '0' && firstChar <= '9')
                    return int.Parse(subtoken, CultureInfo.InvariantCulture);

                if (names == null)
                {
                    throw new ArgumentException(
                        $"\"{subtoken}\" is not a valid crontab field value. " +
                        $"It must be a numeric value between {minValue} and {maxValue} (all inclusive)."
                    );
                }

                for (var i = 0; i < names.Length; ++i)
                {
                    if (names[i].StartsWith(subtoken, StringComparison.InvariantCultureIgnoreCase))
                        return i + minValue;
                }

                throw new ArgumentException(
                    $"\"{subtoken}\" is not a known value name. Use one of the following: {string.Join(", ", names)}."
                );
            }
            
            private void Accumulate(int start, int end, int interval)
            {
                if (start == end) 
                {
                    if (start < 0) 
                    {
                        if (interval <= 1) 
                        {
                            minValueSet = minValue;
                            maxValueSet = maxValue;
                            bits.SetAll(true);
                            return;
                        }

                        start = minValue;
                        end   = maxValue;
                    } 
                    else
                    {
                        if (start < minValue || start > maxValue)
                            throw new ArgumentOutOfRangeException($"{start} is out of [{minValue}, {maxValue}]");
                    }
                } 
                else 
                {
                    if (start > end) 
                    {
                        end   ^= start;
                        start ^= end;
                        end   ^= start;
                    }

                    if (start < 0) 
                        start = minValue;
                    else if (start < minValue) 
                        throw new ArgumentOutOfRangeException($"{start} is out of [{minValue}, {maxValue}]");

                    if (end < 0) 
                        end = maxValue;
                    else if (end > maxValue) 
                        throw new ArgumentOutOfRangeException($"{end} is out of [{minValue}, {maxValue}]");
                }

                if (interval < 1) 
                    interval = 1;

                int i;
                for (i = start - minValue; i <= end - minValue; i += interval) 
                    bits[i] = true;

                if (minValueSet > start) 
                    minValueSet = start;

                i += minValue - interval;

                if (maxValueSet < i) 
                    maxValueSet = i;
            }
            
            public int GetFirst()
                => Next(minValueSet);

            public int Next(int start)
            {
                if (start < minValueSet)
                    start = minValueSet;

                var startIndex = start - minValue;
                var lastIndex  = maxValueSet - minValue;

                for (var i = startIndex; i <= lastIndex; ++i)
                {
                    if (bits[i]) 
                        return i + minValue;
                }

                return Nil;
            }

            public bool Contains(int value)
                => bits[value - minValue];
        }
    }
}