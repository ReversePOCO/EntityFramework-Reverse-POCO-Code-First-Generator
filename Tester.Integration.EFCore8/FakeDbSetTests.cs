using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Generator.Tests.Common;
using NUnit.Framework;
using V8EfrpgTest;

namespace Tester.Integration.EFCore8
{
    [TestFixture]
    [Category(Constants.CI)]
    public class FakeDbSetTests
    {
        private V8EfrpgTest.FakeDbSet<A> _dbSet = null!;
        private List<A> _list = null!;

        [SetUp]
        public void SetUp()
        {
            _dbSet = new V8EfrpgTest.FakeDbSet<A>("AId");

            _list = new List<A>
            {
                new() { AId = 1, C1 = 2, C2 = 3 },
                new() { AId = 4, C1 = 5, C2 = 6 }
            };
        }


        [Test]
        public void AsEnumerable()
        {
            _dbSet.AddRange(_list);
            var result = _dbSet.AsEnumerable();
            Assert.AreEqual(2, result.Count());
        }

        [Test]
        public async Task AsAsyncEnumerable()
        {
            var count = 0;
            await _dbSet.AddRangeAsync(_list);
            await foreach (var y in _dbSet.AsAsyncEnumerable())
                ++count;

            Assert.AreEqual(2, count);
        }

        [Test]
        [TestCase(1, 2, 3)]
        [TestCase(4, 5, 6)]
        public void Find(int id, int c1, int c2)
        {
            _dbSet.AddRange(_list);
            var result = _dbSet.Find(id);

            Assert.IsNotNull(result);
            Assert.AreEqual(id, result?.AId);
            Assert.AreEqual(c1, result?.C1);
            Assert.AreEqual(c2, result?.C2);
        }

        [Test]
        [TestCase(1, 2, 3)]
        [TestCase(4, 5, 6)]
        public async Task FindAsync_CancellationToken(int id, int c1, int c2)
        {
            await _dbSet.AddRangeAsync(_list);
            var cancellationToken = new CancellationToken();
            object[] keyValues = { id };
            var result = await _dbSet.FindAsync(keyValues, cancellationToken);

            Assert.IsNotNull(result);
            Assert.AreEqual(id, result?.AId);
            Assert.AreEqual(c1, result?.C1);
            Assert.AreEqual(c2, result?.C2);
        }
        
        [Test]
        [TestCase(1, 2, 3)]
        [TestCase(4, 5, 6)]
        public async Task FindAsync(int id, int c1, int c2)
        {
            await _dbSet.AddRangeAsync(_list);
            var result = await _dbSet.FindAsync(id);

            Assert.IsNotNull(result);
            Assert.AreEqual(id, result?.AId);
            Assert.AreEqual(c1, result?.C1);
            Assert.AreEqual(c2, result?.C2);
        }

        [Test]
        public void Add()
        {
            _dbSet.Add(_list[0]);
            _dbSet.Add(_list[1]);
            Assert.AreEqual(2, _dbSet.Count());
        }

        [Test]
        public async Task AddAsync()
        {
            await _dbSet.AddAsync(_list[0]);
            await _dbSet.AddAsync(_list[1]);
            Assert.AreEqual(2, _dbSet.Count());
        }

        [Test]
        public void AddRange_params()
        {
            _dbSet.AddRange(_list[0], _list[1]);
            Assert.AreEqual(2, _dbSet.Count());
        }

        [Test]
        public void AddRange_IEnumerable()
        {
            _dbSet.AddRange(_list);
            Assert.AreEqual(2, _dbSet.Count());
        }

        [Test]
        public async Task AddRangeAsync_params()
        {
            await _dbSet.AddRangeAsync(_list[0], _list[1]);
            Assert.AreEqual(2, _dbSet.Count());
        }
        
        [Test]
        public async Task AddRangeAsync_IEnumerable()
        {
            await _dbSet.AddRangeAsync(_list);
            Assert.AreEqual(2, _dbSet.Count());
        }

        [Test]
        public void Attach()
        {
            _dbSet.Attach(_list[0]);
            _dbSet.Attach(_list[1]);
            Assert.AreEqual(2, _dbSet.Count());
        }
        
        [Test]
        public void AttachRange_params()
        {
            _dbSet.AttachRange(_list[0], _list[1]);
            Assert.AreEqual(2, _dbSet.Count());
        }
        
        [Test]
        public void AttachRange_IEnumerable()
        {
            _dbSet.AttachRange(_list);
            Assert.AreEqual(2, _dbSet.Count());
        }

        [Test]
        public void RemoveRange_params()
        {
            _dbSet.RemoveRange(_list[0], _list[1]);
            Assert.AreEqual(0, _dbSet.Count());
        }

        [Test]
        public void RemoveRange_IEnumerable()
        {
            _dbSet.RemoveRange(_list);
            Assert.AreEqual(0, _dbSet.Count());
        }

        [Test]
        public void Update()
        {
            _dbSet.AddRange(_list);

            _list[0].C1 = 987;
            _dbSet.Update(_list[0]);
            Assert.AreEqual(2, _dbSet.Count());

            var result = _dbSet.Find(_list[0].AId);
            Assert.AreEqual(987, result?.C1);
        }

        [Test]
        public void UpdateRange()
        {
            _dbSet.AddRange(_list);

            _list[0].C1 = 987;
            _dbSet.UpdateRange(_list);
            Assert.AreEqual(2, _dbSet.Count());

            var result = _dbSet.Find(_list[0].AId);
            Assert.AreEqual(987, result?.C1);

            result = _dbSet.Find(_list[1].AId);
            Assert.AreEqual(_list[1].C1, result?.C1);
        }

        [Test]
        public void GetList()
        {
            _dbSet.AddRange(_list);
            var result = _dbSet.GetList();
            Assert.AreEqual(_list, result);
        }

        [Test]
        public void GetEnumerator()
        {
            var n = 0;
            foreach (var entity in _dbSet)
            {
                Assert.AreEqual(_list[n++], entity);
            }
        }
    }
}