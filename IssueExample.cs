using Grace.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GraceIocTest
{
    [TestClass]
    public class IssueExample
    {
        [TestMethod]
        public void TestMethod1()
        {
            var container = new DependencyInjectionContainer();
            static ITransientExecutor LocalFactory(RootLocator p) =>
                 (p.GetService(typeof(ISingletonExecutorFactory)) as ISingletonExecutorFactory).CurrentHandler;
            var locator = new RootLocator(container);

            container.Configure(c =>
            {
                c.ExportInstance(locator);

                c.Export<SingletonExecutorFactory>()
                .As<ISingletonExecutorFactory>()
                .Lifestyle.Singleton();

                c.Export<RequestCache>()
                .As<IRequestCache>()
                .Lifestyle.Singleton();

                c.ExportFactory(() => LocalFactory(container.Locate<RootLocator>()))
                .As<ITransientExecutor>();
            });

            // This is registered as a transient in extension code that cannot be changed, but its not really
            container.Forward<ISingletonExecutorFactory, ISingletonExecutorFactoryAsync>();

            using (var scope = container.Locate<RootLocator>().CreateScope())
            {
                var factory = scope.Get<ISingletonExecutorFactory>();
                var executor = scope.Get<ITransientExecutor>();

                // todo: this is the issue as Grace thinks its transient and tries to dispose it
                Assert.AreSame(scope.Get<ISingletonExecutorFactoryAsync>(), factory, "singleton");

                Assert.AreSame(executor, factory.CurrentHandler, "transient");
                Assert.AreSame(scope.Get<ITransientExecutor>(), executor, "transient second");
                Assert.IsTrue(factory is ISingletonExecutorFactory ff && ff.CurrentHandler is TransientExecutorConcrete);
            }

            var sut = container.Locate<ISingletonExecutorFactory>();
            if (!(sut is SingletonExecutorFactory sef))
            {
                throw new InvalidOperationException();
            }

            Assert.IsTrue(sut.CurrentHandler is object);
            Assert.IsFalse(sef.IsWrapperDisposed, "This fails when resolving because it thinks Forward is transient");
        }
    }

    internal static class SampleExtensions
    {
        /// <summary>
        /// Example of extension that cannot be modified
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="services"></param>
        public static void Forward<T1, T2>(
          this DependencyInjectionContainer services)
          where T1 : class
          where T2 : class
        {
            // todo: these always gets set as Transient
            services.Configure(c =>
            {
                c.ExportFactory(() => services.Locate<RootLocator>().Get<T1>() as T2);
            });
        }
    }
}