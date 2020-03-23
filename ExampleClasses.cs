using Grace.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GraceIocTest
{
    public interface IRequestCache
    {
        bool IsActive { get; }
        T Get<T>(string name);
        void Set<T>(string name, T value);
    }

    public class RequestCache : IRequestCache
    {
        public bool IsActive => false;

        public T Get<T>(string name)
        {
            throw new NotImplementedException();
        }

        public void Set<T>(string name, T value)
        {
            throw new NotImplementedException();
        }
    }

    public class Wrapper<T> : IDisposable
    {
        internal bool IsDisposed = false;

        private ThreadLocal<T> _threadLocal;
        private readonly Func<T> _valueFactory;
        private readonly IRequestCache _requestCache;
        private readonly string _name;
        public Wrapper(Guid uniqueId, Func<T> valueFactory, IRequestCache requestCache)
        {
            if (uniqueId == Guid.Empty)
                throw new ArgumentException("uniqueId must be set");
            UniqueId = uniqueId;
            _name = uniqueId.ToString("N");
            _valueFactory = valueFactory;
            _threadLocal = new ThreadLocal<T>(valueFactory);
            _requestCache = requestCache;
        }

        public Guid UniqueId { get; }

        public T Value
        {
            get
            {
                if (!_requestCache.IsActive)
                    return _threadLocal.Value;

                T x = _requestCache.Get<T>(_name);
                if (EqualityComparer<T>.Default.Equals(x, default(T)))
                {
                    x = _valueFactory();
                    _requestCache.Set<T>(_name, x);
                }
                return x;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_threadLocal == null)
                return;
            if (_threadLocal.IsValueCreated)
                (_threadLocal.Value as IDisposable)?.Dispose();
            _threadLocal.Dispose();
            _threadLocal = null;
            IsDisposed = true;
        }
    }

    public interface ISingletonExecutorFactory
    {
        ITransientExecutor CurrentHandler { get; }

        ITransientExecutor CreateDefaultHandler();
    }

    public interface ISingletonExecutorFactoryAsync { }

    public interface ITransientExecutor { }

    public class TransientExecutorConcrete : ITransientExecutor
    {
        public TransientExecutorConcrete(object options)
        {
            Options = options;
        }

        public object Options { get; }
    }

    public class SingletonExecutorFactory : ISingletonExecutorFactory, ISingletonExecutorFactoryAsync, IDisposable
    {
        private readonly Wrapper<ITransientExecutor> _currentHandler;

        public SingletonExecutorFactory(IRequestCache requestCache)
        {
            _currentHandler = new Wrapper<ITransientExecutor>(Guid.NewGuid(), () => CreateDefaultHandler(), requestCache);
        }

        public ITransientExecutor CurrentHandler => _currentHandler.Value;

        public bool IsWrapperDisposed => _currentHandler.IsDisposed;

        public ITransientExecutor CreateDefaultHandler() => Create(new object());

        private ITransientExecutor Create(object options) =>
            new TransientExecutorConcrete(options);

        public void Dispose() => _currentHandler.Dispose();
    }

    public class ScopedLocator : IDisposable, IServiceProvider
    {
        private readonly IExportLocatorScope _scope;

        public ScopedLocator(IExportLocatorScope scope) => _scope = scope;

        public ScopedLocator CreateScope() => new ScopedLocator(_scope.BeginLifetimeScope());

        public void Dispose() => _scope.Dispose();

        public T Get<T>() => _scope.Locate<T>();
        public object GetService(Type serviceType) => _scope.Locate(serviceType);
    }

    public class RootLocator : IDisposable, IServiceProvider
    {
        private readonly IExportLocatorScope _root;

        public RootLocator(IExportLocatorScope root) => _root = root;

        public ScopedLocator CreateScope() => new ScopedLocator(_root.BeginLifetimeScope());

        public void Dispose() => _root.Dispose();

        public object GetService(Type serviceType) => _root.Locate(serviceType);
        public T Get<T>() => _root.Locate<T>();
    }
}