using System;
using UniRx;

namespace Mainboard.Runtime
{
    public sealed class SignalBus : IDisposable
    {
        private readonly Subject<object> _stream = new Subject<object>();

        public void Publish<T>(T signal)
        {
            _stream.OnNext(signal);
        }

        public IObservable<T> Receive<T>()
        {
            return _stream.OfType<object, T>();
        }

        public void Dispose()
        {
            _stream.OnCompleted();
            _stream.Dispose();
        }
    }
}
