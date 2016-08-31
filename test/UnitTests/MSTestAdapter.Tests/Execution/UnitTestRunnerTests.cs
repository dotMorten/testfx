// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.IO;
    using System.Text;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

    using Moq;

    using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
    using global::MSTestAdapter.TestUtilities;

    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTestRunnerTests
    {
        private UnitTestRunner unitTestRunner;

        private Dictionary<string, object> testRunParameters;

        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        [TestInitialize]
        public void TestInit()
        {
            this.unitTestRunner = new UnitTestRunner(false);
            this.testRunParameters = new Dictionary<string, object>();
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();

            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestCleanup]
        public void Testcleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        #region RunSingleTest tests

        [TestMethodV1]
        public void RunSingleTestShouldThrowIfTestMethodIsNull()
        {
            Action a = () => this.unitTestRunner.RunSingleTest(null, null);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethodV1]
        public void RunSingleTestShouldThrowIfTestRunParamtersIsNull()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);
            Action a = () => this.unitTestRunner.RunSingleTest(testMethod, null);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethodV1]
        public void RunSingleTestShouldReturnTestResultIndicateATestNotFoundIfTestMethodCannotBeFound()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());

            var results = this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(UnitTestOutcome.NotFound, results[0].Outcome);
            Assert.AreEqual("Test method M was not found.", results[0].ErrorMessage);
        }

        [TestMethodV1]
        public void RunSingleTestShouldReturnTestResultIndicatingNotRunnableTestIfTestMethodCannotBeRun()
        {
            var type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());

            var results = this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            var expectedMessage = string.Format(
                "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(UnitTestOutcome.NotRunnable, results[0].Outcome);
            Assert.AreEqual(expectedMessage, results[0].ErrorMessage);
        }

        [TestMethodV1]
        public void RunSingleTestShouldReturnTestResultIndicatingFailureIfThereIsAnyTypeInspectionExceptionWhenInspectingTestMethod()
        {
            var type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
            var testMethod = new TestMethod("ImaginaryTestMethod", type.FullName, "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());

            var results = this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            var expectedMessage = string.Format(
                "Method {0}.{1} does not exist.",
                testMethod.FullClassName,
                testMethod.Name);

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(UnitTestOutcome.Failed, results[0].Outcome);
            Assert.AreEqual(expectedMessage, results[0].ErrorMessage);
        }

        [TestMethodV1]
        public void RunSingleTestShouldReturnTestResultsForAPassingTestMethod()
        {
            var type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());

            var results = this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(UnitTestOutcome.Passed, results[0].Outcome);
            Assert.IsNull(results[0].ErrorMessage);
        }

        [TestMethodV1]
        public void RunSingleTestShouldSetTestsAsInProgressInTestContext()
        {
            var type = typeof(DummyTestClass);
            var methodInfo = type.GetMethod("TestMethodToTestInProgress");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());

            // Asserting in the test method execution flow itself.
            var results = this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(UnitTestOutcome.Passed, results[0].Outcome);
        }

        [TestMethodV1]
        public void RunSingleTestShouldCallAssemblyInitializeAndClassInitializeMethodsInOrder()
        {
            var mockReflectHelper = new Mock<ReflectHelper>();
            this.unitTestRunner = new UnitTestRunner(false, mockReflectHelper.Object);

            var type = typeof(DummyTestClassWithInitializeMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());
            mockReflectHelper.Setup(
                rh =>
                rh.IsAttributeDefined(
                    type.GetMethod("AssemblyInitialize"),
                    typeof(UTF.AssemblyInitializeAttribute),
                    It.IsAny<bool>())).Returns(true);

            var validator = 1;
            DummyTestClassWithInitializeMethods.AssemblyInitializeMethodBody = () => { validator = validator << 2; };
            DummyTestClassWithInitializeMethods.ClassInitializeMethodBody = () => { validator = validator >> 2; };

            this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            Assert.AreEqual(1, validator);
        }

        #endregion

        #region RunCleanup Tests

        [TestMethodV1]
        public void RunCleanupShouldReturnNullOnNoCleanUpMethods()
        {   
            Assert.IsNull(this.unitTestRunner.RunCleanup());
        }

        [TestMethodV1]
        public void RunCleanupShouldReturnCleanupResultsForAssemblyAndClassCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());

            this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            var assemblyCleanupCount = 0;
            var classCleanupCount = 0;

            DummyTestClassWithCleanupMethods.AssemblyCleanupMethodBody = () =>
                {
                    assemblyCleanupCount++;
                    throw new NotImplementedException();
                };

            DummyTestClassWithCleanupMethods.ClassCleanupMethodBody = () =>
            {
                classCleanupCount++;
                throw new NotImplementedException();
            };

            var cleanupresult = this.unitTestRunner.RunCleanup();
            
            Assert.AreEqual(1, assemblyCleanupCount);
            Assert.AreEqual(1, classCleanupCount);
            Assert.AreEqual(2, cleanupresult.Warnings.Count());
            Assert.IsTrue(cleanupresult.Warnings.All(w => w.Contains("NotImplemented")));
        }

        [TestMethodV1]
        public void RunCleanupShouldReturnCleanupResultsWithDebugTraceLogsSetIfDebugTraceEnabled()
        {
            unitTestRunner = new UnitTestRunner(true);
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());

            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            var cleanupresult = this.unitTestRunner.RunCleanup();
            Assert.AreEqual(cleanupresult.DebugTrace, "DummyTrace");
        }

        [TestMethodV1]
        public void RunCleanupShouldReturnCleanupResultsWithNoDebugAndTraceLogsSetIfDebugTraceDisabled()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
                .Returns(Assembly.GetExecutingAssembly());

            StringWriter writer = new StringWriter(new StringBuilder("DummyTrace"));
            this.testablePlatformServiceProvider.MockTraceListener.Setup(tl => tl.GetWriter()).Returns(writer);

            this.unitTestRunner.RunSingleTest(testMethod, this.testRunParameters);

            var cleanupresult = this.unitTestRunner.RunCleanup();
            Assert.AreEqual(cleanupresult.DebugTrace, string.Empty);
        }

        #endregion

        [UTF.TestClass]
        private class DummyTestClass
        {
            public UTFExtension.TestContext TestContext { get; set; }

            [UTF.TestMethod]
            public void TestMethodToTestInProgress()
            {
                Assert.AreEqual(UTF.UnitTestOutcome.InProgress, this.TestContext.CurrentTestOutcome);
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithInitializeMethods
        {
            public static Action AssemblyInitializeMethodBody { get; set; }

            public static Action ClassInitializeMethodBody { get; set; }

            // The reflectHelper instance would set the AssemblyInitialize attribute here before running any tests.
            // Setting an attribute causes conflicts with other tests.
            public static void AssemblyInitialize(UTFExtension.TestContext tc)
            {
                AssemblyInitializeMethodBody.Invoke();
            }

            [UTF.ClassInitialize]
            public static void ClassInitialize(UTFExtension.TestContext tc)
            {
                ClassInitializeMethodBody.Invoke();
            }

            [UTF.TestMethod]
            public void TestMethod()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithCleanupMethods
        {
            public static Action AssemblyCleanupMethodBody { get; set; }
            
            public static Action ClassCleanupMethodBody { get; set; }

            [UTF.AssemblyCleanup]
            public static void AssemblyCleanup()
            {
                if(AssemblyCleanupMethodBody != null)
                    AssemblyCleanupMethodBody.Invoke();
            }

            [UTF.ClassCleanup]
            public static void ClassCleanup()
            {
                ClassCleanupMethodBody.Invoke();
            }

            [UTF.TestMethod]
            public void TestMethod()
            {
            }
        }
    }
}