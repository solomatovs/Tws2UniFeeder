using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Text;

namespace Tws2UniFeeder
{
    public static class ConversionUniFeederStringEx
    {
        public static byte[] ToUniFeederByteArray(this string s) => Encoding.UTF8.GetBytes(s + "\r\n");
        public static byte[] ToByteArrayWithZeroEnd(this string s) => Encoding.UTF8.GetBytes(s + "\0");
        public static IEnumerable<string> ToUniFeederStrings(this IEnumerable<byte> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            using (var ms = new MemoryStream())
            {
                byte prev_b = 0;
                foreach (var b in source)
                {
                    if (IsUniFeederDivider(b, prev_b))
                    {
                        yield return GetUnFeederString(ms);
                        ms.SetLength(0);
                        prev_b = 0;
                    }
                    else
                        ms.WriteByte(b);

                    prev_b = b;
                }
                if (ms.Position != 0)
                    throw new InvalidDataException("ToStrings: no termination(1).");
            }
        }

        public static IObservable<string> ToUniFeederStrings(this IObservable<byte> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var ms = new MemoryStream();

            return Observable.Create<string>(observer =>
            {
                byte prev_b = 0;
                return source.Subscribe(
                    onNext: b =>
                    {
                        if (IsUniFeederDivider(b, prev_b))
                        {
                            observer.OnNext(GetUnFeederString(ms));
                            ms.SetLength(0);
                            prev_b = 0;
                        }
                        else
                            ms.WriteByte(b);

                        prev_b = b;
                    },
                    onError: observer.OnError,
                    onCompleted: () =>
                    {
                        if (ms.Position == 0)
                            observer.OnCompleted();
                        else
                            observer.OnError(new InvalidDataException("ToStrings: no termination(2)."));

                        ms.Dispose();
                    });
            });
        }

        private static string GetUnFeederString(in MemoryStream ms) => Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Position - 1);
        private static bool IsUniFeederDivider(byte c, byte prev_b)
        {
            return c == 10 && prev_b == 13;
        }
    
    }
}
