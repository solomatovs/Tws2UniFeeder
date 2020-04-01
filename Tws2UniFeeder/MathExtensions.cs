using System;
using System.Collections.Generic;
using System.Linq;

namespace Tws2UniFeeder
{
    public static class MathExtensions
    {
        /// <summary>
        /// (Стандартное отклонение, Средняя)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static (double, double) StandardDeviationAndAverage<T>(this IEnumerable<T> enumerable, Func<T, double> selector)
        {
            double sum = 0;
            double average = enumerable.Average(selector);
            int N = 0;
            foreach (T item in enumerable)
            {
                double diff = selector(item) - average;
                sum += diff * diff;
                N++;
            }
            return (N == 0 ? 0 : Math.Sqrt(sum / N), average);
        }
        /// <summary>
        /// Возвращает последовательность в которой будут отсутствовать элементы свыше указанной сигмы k
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="k"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> SkipOutliers<T>(this IEnumerable<T> enumerable, double k, Func<T, double> selector)
        {
            double sum = 0;
            double average = enumerable.Average(selector);
            int N = 0;
            foreach (T item in enumerable)
            {
                double diff = selector(item) - average;
                sum += diff * diff;
                N++;
            }
            double SD = N == 0 ? 0 : Math.Sqrt(sum / N);
            double delta = k * SD;
            foreach (T item in enumerable)
            {
                if (Math.Abs(selector(item) - average) <= delta)
                    yield return item;
            }
        }

        /// <summary>
        /// Возвращает номер сигмы в пределах которой расположено значение элемента. Номер сигмы начинается с 1
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable">последовательность</param>
        /// <param name="element">элемент который сравнивается с последовательностью</param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static int Sigma<T>(this IEnumerable<T> enumerable, T element, Func<T, double> selector)
        {
            double sum = 0;
            double average = enumerable.Average(selector);
            int N = 0;
            foreach (T item in enumerable)
            {
                double diff = selector(item) - average;
                sum += diff * diff;
                N++;
            }

            double SD = N == 0 ? 0 : Math.Sqrt(sum / N);
            return (int)Math.Truncate(Math.Abs(selector(element) - average) / SD) + 1;
        }

        //public static IEnumerable<KeyValuePair<T, T>> CrossJoin<T>(this IEnumerable<T> enumerable, Func<T, double> selector)
        //{
        //    int i = 0;
        //    foreach (var l in LastTicks)
        //    {
        //        foreach (var r in LastTicks)
        //        {

        //        }
        //    }
        //}
    }
}
