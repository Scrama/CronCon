using System;

namespace croncon
{
    class Program
    {
        static void Main(string[] args)
        {
            var now = DateTime.Now;
            var i = 10;
            try
            {
                while (--i > 0)
                {
                    now = CronCalc.NextFire("10 0-8/2 * * SUN,TUE", now);
                    Console.WriteLine(now.ToString("yyyy.MM.dd HH:mm:ss") + "  " + now.DayOfWeek.ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message, e);
            }
        }
    }
}