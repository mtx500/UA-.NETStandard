/* ========================================================================
 * Copyright (c) 2005-2020 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 *
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using NUnit.Framework;
using Opc.Ua.Server.Tests;
using Assert = NUnit.Framework.Legacy.ClassicAssert;


namespace Opc.Ua.Client.Tests
{
    /// <summary>
    /// Client tests.
    /// </summary>
    [TestFixture, Category("Client"), Category("NodeCache")]
    [SetCulture("en-us"), SetUICulture("en-us")]
    [TestFixtureSource(nameof(FixtureArgs))]
    [MemoryDiagnoser]
    [DisassemblyDiagnoser]
    public class NodeCacheTest : ClientTestFramework
    {
        private const int kTestSetSize = 100;

        public NodeCacheTest(string uriScheme = Utils.UriSchemeOpcTcp) :
            base(uriScheme)
        {
        }

        #region Test Setup
        /// <summary>
        /// Set up a Server and a Client instance.
        /// </summary>
        [OneTimeSetUp]
        public new Task OneTimeSetUp()
        {
            SupportsExternalServerUrl = true;
            // create a new session for every test
            SingleSession = false;
            return base.OneTimeSetUp();
        }

        /// <summary>
        /// Tear down the Server and the Client.
        /// </summary>
        [OneTimeTearDown]
        public new Task OneTimeTearDownAsync()
        {
            return base.OneTimeTearDownAsync();
        }

        /// <summary>
        /// Test setup.
        /// </summary>
        [SetUp]
        public new async Task SetUp()
        {
            await base.SetUp().ConfigureAwait(false);

            // clear node cache
            Session.NodeCache.Clear();
        }

        /// <summary>
        /// Test teardown.
        /// </summary>
        [TearDown]
        public new Task TearDown()
        {
            return base.TearDown();
        }
        #endregion

        #region Benchmark Setup
        /// <summary>
        /// Global Setup for benchmarks.
        /// </summary>
        [GlobalSetup]
        public new void GlobalSetup()
        {
            base.GlobalSetup();
        }

        /// <summary>
        /// Global cleanup for benchmarks.
        /// </summary>
        [GlobalCleanup]
        public new void GlobalCleanup()
        {
            base.GlobalCleanup();
        }
        #endregion

        #region Test Methods
        /// <summary>
        /// Load Ua types in node cache.
        /// </summary>
        [Test, Order(500)]
        public void NodeCache_LoadUaDefinedTypes()
        {
            INodeCache nodeCache = Session.NodeCache;
            Assert.IsNotNull(nodeCache);

            // load the predefined types
            nodeCache.LoadUaDefinedTypes(Session.SystemContext);

            // reload the predefined types
            nodeCache.LoadUaDefinedTypes(Session.SystemContext);
        }

        /// <summary>
        /// Browse all variables in the objects folder.
        /// </summary>
        [Test, Order(100)]
        public void NodeCache_BrowseAllVariables()
        {
            var result = new List<INode>();
            var nodesToBrowse = new ExpandedNodeIdCollection {
                ObjectIds.ObjectsFolder
            };

            Session.FetchTypeTree(ReferenceTypeIds.References);

            while (nodesToBrowse.Count > 0)
            {
                var nextNodesToBrowse = new ExpandedNodeIdCollection();
                foreach (var node in nodesToBrowse)
                {
                    try
                    {
                        var organizers = Session.NodeCache.FindReferences(
                            node,
                            ReferenceTypeIds.HierarchicalReferences,
                            false,
                            true);
                        nextNodesToBrowse.AddRange(organizers.Select(n => n.NodeId));
                        var objectNodes = organizers.Where(n => n is ObjectNode);
                        var variableNodes = organizers.Where(n => n is VariableNode);
                        result.AddRange(variableNodes);
                    }
                    catch (ServiceResultException sre)
                    {
                        if (sre.StatusCode == StatusCodes.BadUserAccessDenied)
                        {
                            TestContext.Out.WriteLine($"Access denied: Skip node {node}.");
                        }
                    }
                }
                nodesToBrowse = new ExpandedNodeIdCollection(nextNodesToBrowse.Distinct());
                TestContext.Out.WriteLine("Found {0} duplicates", nextNodesToBrowse.Count - nodesToBrowse.Count);
            }

            TestContext.Out.WriteLine("Found {0} variables", result.Count);
        }

        /// <summary>
        /// Browse all variables in the objects folder.
        /// </summary>
        [Test, Order(200)]
        public void NodeCache_BrowseAllVariables_MultipleNodes()
        {
            var result = new List<INode>();
            var nodesToBrowse = new ExpandedNodeIdCollection {
                ObjectIds.ObjectsFolder
            };

            Session.FetchTypeTree(ReferenceTypeIds.References);
            var referenceTypeIds = new NodeIdCollection() { ReferenceTypeIds.HierarchicalReferences };
            while (nodesToBrowse.Count > 0)
            {
                var nextNodesToBrowse = new ExpandedNodeIdCollection();
                try
                {
                    var organizers = Session.NodeCache.FindReferences(
                        nodesToBrowse,
                        referenceTypeIds,
                        false,
                        true);
                    nextNodesToBrowse.AddRange(organizers.Select(n => n.NodeId));
                    var objectNodes = organizers.Where(n => n is ObjectNode);
                    var variableNodes = organizers.Where(n => n is VariableNode);
                    result.AddRange(variableNodes);
                }
                catch (ServiceResultException sre)
                {
                    if (sre.StatusCode == StatusCodes.BadUserAccessDenied)
                    {
                        TestContext.Out.WriteLine($"Access denied: Skipped node.");
                    }
                }
                nodesToBrowse = new ExpandedNodeIdCollection(nextNodesToBrowse.Distinct());
                TestContext.Out.WriteLine("Found {0} duplicates", nextNodesToBrowse.Count - nodesToBrowse.Count);
            }

            TestContext.Out.WriteLine("Found {0} variables", result.Count);
        }

        /// <summary>
        /// Load Ua types in node cache.
        /// </summary>
        [Test, Order(500)]
        public void NodeCache_References()
        {
            INodeCache nodeCache = Session.NodeCache;
            Assert.IsNotNull(nodeCache);

            // ensure the predefined types are loaded
            nodeCache.LoadUaDefinedTypes(Session.SystemContext);

            // check on all reference type ids
            var refTypeDictionary = typeof(ReferenceTypeIds).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(NodeId))
                .ToDictionary(f => f.Name, f => (NodeId)f.GetValue(null));

            TestContext.Out.WriteLine("Testing {0} references", refTypeDictionary.Count);
            foreach (var property in refTypeDictionary)
            {
                TestContext.Out.WriteLine("FindReferenceTypeName({0})={1}", property.Value, property.Key);
                // find the Qualified Name
                var qn = nodeCache.FindReferenceTypeName(property.Value);
                Assert.NotNull(qn);
                Assert.AreEqual(property.Key, qn.Name);
                // find the node by name
                var refId = nodeCache.FindReferenceType(new QualifiedName(property.Key));
                Assert.NotNull(refId);
                Assert.AreEqual(property.Value, refId);
                // is the node id known?
                var isKnown = nodeCache.IsKnown(property.Value);
                Assert.IsTrue(isKnown);
                // is it a reference?
                var isTypeOf = nodeCache.IsTypeOf(
                    NodeId.ToExpandedNodeId(refId, Session.NamespaceUris),
                    NodeId.ToExpandedNodeId(ReferenceTypeIds.References, Session.NamespaceUris));
                Assert.IsTrue(isTypeOf);
                // negative test
                isTypeOf = nodeCache.IsTypeOf(
                    NodeId.ToExpandedNodeId(refId, Session.NamespaceUris),
                    NodeId.ToExpandedNodeId(DataTypeIds.Byte, Session.NamespaceUris));
                Assert.IsFalse(isTypeOf);
                var subTypes = nodeCache.FindSubTypes(NodeId.ToExpandedNodeId(refId, Session.NamespaceUris));
                Assert.NotNull(subTypes);
            }
        }

        [Test, Order(720)]
        public void NodeCacheFind()
        {
            if (ReferenceDescriptions == null)
            {
                BrowseFullAddressSpace();
            }

            foreach (var reference in ReferenceDescriptions.Take(MaxReferences))
            {
                var nodeId = ExpandedNodeId.ToNodeId(reference.NodeId, Session.NamespaceUris);
                var node = Session.NodeCache.Find(reference.NodeId);
                TestContext.Out.WriteLine("NodeId: {0} Node: {1}", nodeId, node);
            }
        }

        [Test, Order(730)]
        public void NodeCacheFetchNode()
        {
            if (ReferenceDescriptions == null)
            {
                BrowseFullAddressSpace();
            }

            foreach (var reference in ReferenceDescriptions.Take(MaxReferences))
            {
                var nodeId = ExpandedNodeId.ToNodeId(reference.NodeId, Session.NamespaceUris);
                var node = Session.NodeCache.FetchNode(reference.NodeId);
                TestContext.Out.WriteLine("NodeId: {0} Node: {1}", nodeId, node);
            }
        }

        [Test, Order(740)]
        public void NodeCacheFetchNodes()
        {
            if (ReferenceDescriptions == null)
            {
                BrowseFullAddressSpace();
            }

            var testSet = ReferenceDescriptions.Take(MaxReferences).Select(r => r.NodeId).ToList();
            IList<Node> nodeCollection = Session.NodeCache.FetchNodes(testSet);

            foreach (var node in nodeCollection)
            {
                var nodeId = ExpandedNodeId.ToNodeId(node.NodeId, Session.NamespaceUris);
                TestContext.Out.WriteLine("NodeId: {0} Node: {1}", nodeId, node);
            }
        }

        [Test, Order(750)]
        public void NodeCacheFindReferences()
        {
            if (ReferenceDescriptions == null)
            {
                BrowseFullAddressSpace();
            }

            var testSet = ReferenceDescriptions.Take(MaxReferences).Select(r => r.NodeId).ToList();
            IList<INode> nodes = Session.NodeCache.FindReferences(testSet, new NodeIdCollection() { ReferenceTypeIds.NonHierarchicalReferences }, false, true);

            foreach (var node in nodes)
            {
                var nodeId = ExpandedNodeId.ToNodeId(node.NodeId, Session.NamespaceUris);
                TestContext.Out.WriteLine("NodeId: {0} Node: {1}", nodeId, node);
            }
        }


        [Test, Order(900)]
        public void FetchTypeTree()
        {
            Session.FetchTypeTree(NodeId.ToExpandedNodeId(DataTypeIds.BaseDataType, Session.NamespaceUris));
        }

        [Test, Order(910)]
        public void FetchAllReferenceTypes()
        {
            var bindingFlags =
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public;
            var fieldValues = typeof(ReferenceTypeIds)
                .GetFields(bindingFlags)
                .Select(field => NodeId.ToExpandedNodeId((NodeId)field.GetValue(null), Session.NamespaceUris));

            Session.FetchTypeTree(new ExpandedNodeIdCollection(fieldValues));
        }

        /// <summary>
        /// Test concurrent access of FetchNodes.
        /// </summary>
        [Test, Order(1000)]
        public void NodeCacheFetchNodesConcurrent()
        {
            if (ReferenceDescriptions == null)
            {
                BrowseFullAddressSpace();
            }

            Random random = new Random(62541);
            var testSet = ReferenceDescriptions.OrderBy(o => random.Next()).Take(kTestSetSize).Select(r => r.NodeId).ToList();
            var taskList = new List<Task>();

            // test concurrent access of FetchNodes
            for (int i = 0; i < 10; i++)
            {
                Task t = Task.Run(
                    () => {
                        IList<Node> nodeCollection = Session.NodeCache.FetchNodes(testSet);
                    }
                    );
                taskList.Add(t);
            }
            Task.WaitAll(taskList.ToArray());
        }

        /// <summary>
        /// Test concurrent access of Find.
        /// </summary>
        [Test, Order(1100)]
        public void NodeCacheFindNodesConcurrent()
        {
            if (ReferenceDescriptions == null)
            {
                BrowseFullAddressSpace();
            }

            Random random = new Random(62541);
            var testSet = ReferenceDescriptions.OrderBy(o => random.Next()).Take(kTestSetSize).Select(r => r.NodeId).ToList();
            var taskList = new List<Task>();

            // test concurrent access of FetchNodes
            for (int i = 0; i < 10; i++)
            {
                Task t = Task.Run(() => {
                    IList<INode> nodeCollection = Session.NodeCache.Find(testSet);
                });
                taskList.Add(t);
            }
            Task.WaitAll(taskList.ToArray());
        }

        /// <summary>
        /// Test concurrent access of FindReferences.
        /// </summary>
        [Test, Order(1200)]
        public void NodeCacheFindReferencesConcurrent()
        {
            if (ReferenceDescriptions == null)
            {
                BrowseFullAddressSpace();
            }

            Random random = new Random(62541);
            var testSet = ReferenceDescriptions.OrderBy(o => random.Next()).Take(kTestSetSize).Select(r => r.NodeId).ToList();
            var taskList = new List<Task>();
            var refTypeIds = new List<NodeId>() { ReferenceTypeIds.HierarchicalReferences };
            FetchAllReferenceTypes();

            // test concurrent access of FetchNodes
            for (int i = 0; i < 10; i++)
            {
                Task t = Task.Run(() => {
                    IList<INode> nodeCollection = Session.NodeCache.FindReferences(testSet, refTypeIds, false, true);
                });
                taskList.Add(t);
            }
            Task.WaitAll(taskList.ToArray());
        }

        /// <summary>
        /// Test concurrent access of many methods in INodecache interface
        /// </summary>
        [Test, Order(1300)]
        public void NodeCacheTestAllMethodsConcurrently()
        {
            const int testCases = 10;
            const int testCaseRunTime = 5_000;

            if (ReferenceDescriptions == null)
            {
                BrowseFullAddressSpace();
            }

            Random random = new Random(62541);
            var testSetAll = ReferenceDescriptions.OrderBy(o => random.Next()).Where(r=>r.NodeClass == NodeClass.Variable).Select(r => r.NodeId).ToList();
            var testSet1 = testSetAll.Take(kTestSetSize).ToList();
            var testSet2 = testSetAll.Skip(kTestSetSize).Take(kTestSetSize).ToList();
            var testSet3 = testSetAll.Skip(kTestSetSize * 2).Take(kTestSetSize).ToList();

            var taskList = new List<Task>();
            var refTypeIds = new List<NodeId>() { ReferenceTypeIds.HierarchicalReferences };

            // test concurrent access of many methods in INodecache interface
            for (int i = 0; i < testCases; i++)
            {
                int iteration = i;
                Task t = Task.Run(() => {
                    DateTime start = DateTime.UtcNow;
                    do
                    {
                        switch (iteration)
                        {
                            case 0:
                                FetchAllReferenceTypes();
                                IList<INode> result = Session.NodeCache.FindReferences(testSet1, refTypeIds, false, true);
                                break;
                            case 1:
                                IList<INode> result1 = Session.NodeCache.Find(testSet2);
                                break;
                            case 2:
                                IList<Node> result2 = Session.NodeCache.FetchNodes(testSet3);
                                string displayText = Session.NodeCache.GetDisplayText(result2[0]);
                                break;
                            case 3:
                                IList<INode> result3 = Session.NodeCache.FindReferences(testSet1[0], refTypeIds[0], false, true);
                                break;
                            case 4:
                                INode result4 = Session.NodeCache.Find(testSet2[0]);
                                Assert.NotNull(result4);
                                Assert.True(result4 is VariableNode);
                                break;
                            case 5:
                                Node result5 = Session.NodeCache.FetchNode(testSet3[0]);
                                Assert.NotNull(result5);
                                Assert.True(result5 is VariableNode);
                                Session.NodeCache.FetchSuperTypes(result5.NodeId);
                                break;
                            case 6:
                                string text = Session.NodeCache.GetDisplayText(testSet2[0]);
                                Assert.NotNull(text);
                                break;
                            case 7:
                                NodeId number = new NodeId((int)BuiltInType.Number);
                                bool isKnown = Session.NodeCache.IsKnown(new ExpandedNodeId((int)BuiltInType.Int64));
                                Assert.True(isKnown);
                                bool isKnown2 = Session.NodeCache.IsKnown(TestData.DataTypeIds.ScalarStructureDataType);
                                Assert.True(isKnown2);
                                NodeId nodeId = Session.NodeCache.FindSuperType(TestData.DataTypeIds.Vector);
                                Assert.AreEqual(DataTypeIds.Structure, nodeId);
                                NodeId nodeId2 = Session.NodeCache.FindSuperType(ExpandedNodeId.ToNodeId(TestData.DataTypeIds.Vector, Session.NamespaceUris));
                                Assert.AreEqual(DataTypeIds.Structure, nodeId2);
                                IList<NodeId> subTypes = Session.NodeCache.FindSubTypes(new ExpandedNodeId((int)BuiltInType.Number));
                                bool isTypeOf = Session.NodeCache.IsTypeOf(new ExpandedNodeId((int)BuiltInType.Int32), new ExpandedNodeId((int)BuiltInType.Number));
                                bool isTypeOf2 = Session.NodeCache.IsTypeOf(new NodeId((int)BuiltInType.UInt32), number);
                                break;
                            case 8:
                                bool isEncodingOf = Session.NodeCache.IsEncodingOf(new ExpandedNodeId((int)BuiltInType.Int32), DataTypeIds.Structure);
                                Assert.False(isEncodingOf);
                                bool isEncodingFor = Session.NodeCache.IsEncodingFor(DataTypeIds.Structure,
                                    new TestData.ScalarStructureDataType());
                                Assert.True(isEncodingFor);
                                bool isEncodingFor2 = Session.NodeCache.IsEncodingFor(new NodeId((int)BuiltInType.UInt32), new NodeId((int)BuiltInType.UInteger));
                                Assert.False(isEncodingFor2);
                                break;
                            case 9:
                                NodeId findDataTypeId = Session.NodeCache.FindDataTypeId(new ExpandedNodeId((int)Objects.DataTypeAttributes_Encoding_DefaultBinary));
                                NodeId findDataTypeId2 = Session.NodeCache.FindDataTypeId((int)Objects.DataTypeAttributes_Encoding_DefaultBinary);
                                break;
                            default:
                                Assert.Fail("Invalid test case");
                                break;
                        }
                    } while ((DateTime.UtcNow - start).TotalMilliseconds < testCaseRunTime);

                });
                taskList.Add(t);
            }
            Task.WaitAll(taskList.ToArray());
        }
        #endregion

        #region Benchmarks
        #endregion

        #region Private Methods
        private void BrowseFullAddressSpace()
        {
            var requestHeader = new RequestHeader {
                Timestamp = DateTime.UtcNow,
                TimeoutHint = MaxTimeout
            };

            // Session
            var clientTestServices = new ClientTestServices(Session);
            ReferenceDescriptions = CommonTestWorkers.BrowseFullAddressSpaceWorker(clientTestServices, requestHeader);
        }
        #endregion
    }
}
