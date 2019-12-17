using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Text;

namespace tws2uni
{
    public static class ConversionUniFeederStringEx
    {
        public static byte[] ToUniFeederByteArray(this string s) => Encoding.UTF8.GetBytes(s + "\0");

        public static IEnumerable<string> ToUniFeederStrings(this IEnumerable<byte> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            using (var ms = new MemoryStream())
            {
                foreach (var b in source)
                {
                    if (b == 0)
                    {
                        yield return GetString(ms);
                        ms.SetLength(0);
                    }
                    else
                        ms.WriteByte(b);
                }
                if (ms.Position != 0)
                    throw new InvalidDataException("ToStrings: no termination(1).");
            }
        }

        public static IObservable<string> ToUniFeederStrings(this IObservable<byte> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return Observable.Create<string>(observer =>
            {
                var ms = new MemoryStream();
                return source.Subscribe(
                    onNext: b =>
                    {
                        if (b.isUniFeederDivider())
                        {
                            observer.OnNext(GetString(ms));
                            ms.SetLength(0);
                        }
                        else
                            ms.WriteByte(b);
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

        private static string GetString(in MemoryStream ms) => Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Position);
        private static bool isUniFeederDivider(this byte c) => c == 13;
    }
}
