using System;
using System.Diagnostics;
using System.Threading;

namespace ReadWriteLock
{
    /**
 * 可重入读写锁性能测试
 * ReaderWriterLockSlim作为 Baseline做性能比较
 *
 * 测试条件和参数：
 * 读写线程总数：1024个
 * 假设读取时间：10ms,写入时间：100ms
 * 写者比例：5%，利用随机数产生器控制读写者比例，并记录随机数，保证两次实验随机数相同
 * 当 1024个线程全部执行结束后输出读者和写着的平均等待时间
 *
 * 测试结果：
 * Ours所耗总时间5448ms
 * Ours读者等待时间：4556031ms，Ours写者等待时间114165ms
 * Ours读者平均等待时间：4668ms，Ours写者平均等待时间2378ms
 * Baseline所耗总时间5484ms
 * Baseline读者等待时间：4690889ms，Baseline写者等待时间128164ms
 * Baseline读者平均等待时间：4806ms，Baseline写者平均等待时间2670ms
 * 
 * 小优
 */
    public class TestCase2
    {
        private long readWaitTime;
        private long writeWaitTime;

        private int readerThreadNum;
        private int writerThreadNum;
        private int totalThreadNum;

        private int finishedWorkerCount;
        private AutoResetEvent finished;

        private ReentrantReaderWriterLock rwLock;
        private ReaderWriterLockSlim rwLockSlim;

        public TestCase2()
        {
            readWaitTime = 0;
            writeWaitTime = 0;

            readerThreadNum = 0;
            writerThreadNum = 0;
            totalThreadNum = 1024;

            finishedWorkerCount = 0;
            finished = new AutoResetEvent(false);

            rwLock = new ReentrantReaderWriterLock();
            rwLockSlim = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        private static void Reader(TestCase2 testCase)
        {
            Stopwatch stopwatch = new Stopwatch();
            // 记录获取读锁的等待时间
            stopwatch.Start();
            testCase.rwLock.EnterReadLock();
            stopwatch.Stop();
            // 原子操作，更新读者等待总时间
            Interlocked.Add(ref testCase.readWaitTime, stopwatch.ElapsedMilliseconds);
            // 模仿读操作用时
            Thread.Sleep(10);
            // 释放读锁
            testCase.rwLock.ExitReadLock();
            Interlocked.Increment(ref testCase.finishedWorkerCount);
            if (testCase.finishedWorkerCount == testCase.totalThreadNum)
            {
                testCase.finished.Set();
            }
        }

        private static void Writer(TestCase2 testCase)
        {
            Stopwatch stopwatch = new Stopwatch();
            // 记录获取写锁的等待时间
            stopwatch.Start();
            testCase.rwLock.EnterWriteLock();
            stopwatch.Stop();
            // 原子操作，更新写者等待总时间
            Interlocked.Add(ref testCase.writeWaitTime, stopwatch.ElapsedMilliseconds);
            // 模仿写操作用时
            Thread.Sleep(100);
            // 释放写锁
            testCase.rwLock.ExitWriteLock();
            Interlocked.Increment(ref testCase.finishedWorkerCount);
            if (testCase.finishedWorkerCount == testCase.totalThreadNum)
            {
                testCase.finished.Set();
            }
        }

        private static void ReaderBaseline(TestCase2 testCase)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            testCase.rwLockSlim.EnterReadLock();
            stopwatch.Stop();
            // 原子操作，更新读者等待总时间
            Interlocked.Add(ref testCase.readWaitTime, stopwatch.ElapsedMilliseconds);
            // 模仿读操作用时
            Thread.Sleep(10);
            testCase.rwLockSlim.ExitReadLock();
            Interlocked.Increment(ref testCase.finishedWorkerCount);
            if (testCase.finishedWorkerCount == testCase.totalThreadNum)
            {
                testCase.finished.Set();
            }
        }

        private static void WriterBaseline(TestCase2 testCase)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            testCase.rwLockSlim.EnterWriteLock();
            stopwatch.Stop();
            // 原子操作，更新写者等待总时间
            Interlocked.Add(ref testCase.writeWaitTime, stopwatch.ElapsedMilliseconds);
            // 模仿写操作用时
            Thread.Sleep(100);
            testCase.rwLockSlim.ExitWriteLock();
            Interlocked.Increment(ref testCase.finishedWorkerCount);
            if (testCase.finishedWorkerCount == testCase.totalThreadNum)
            {
                testCase.finished.Set();
            }
        }

        private void PrintTestResult(Stopwatch stopwatch, String lockName)
        {
            Console.WriteLine(lockName + "所耗总时间{0}ms", stopwatch.ElapsedMilliseconds);
            Console.WriteLine(lockName + "读者等待时间：{0}ms，" + lockName + "写者等待时间{1}ms", readWaitTime, writeWaitTime);
            Console.WriteLine(lockName + "读者平均等待时间：{0}ms，" + lockName + "写者平均等待时间{1}ms", readWaitTime / readerThreadNum,
                writeWaitTime / writerThreadNum);
        }

        public void Test()
        {
            System.Console.WriteLine("\nTest Case 2 start!\n");

            // 使用stopwatch计算耗时
            Stopwatch stopwatch = new Stopwatch();
            var rand = new Random();
            int[] randNumList = new int[totalThreadNum];
            // 使用随机数，模拟5%的线程为写者线程，同时记录随机数以保证两次测试的公平性
            for (int i = 0; i < totalThreadNum; i++)
                randNumList[i] = rand.Next(20);

            // 使用我们自己实现的 ReentrantReaderWriterLock 进行测试
            stopwatch.Start();
            for (int i = 0; i < totalThreadNum; i++)
            {
                int randNum = randNumList[i];
                // randNum范围是0-19，5%的线程为写者
                if (randNum == 0)
                {
                    writerThreadNum++;
                    new Thread(() => Writer(this)).Start();
                }
                else
                {
                    readerThreadNum++;
                    new Thread(() => Reader(this)).Start();
                }
            }

            finished.WaitOne();
            stopwatch.Stop();
            // 输出测试结果
            PrintTestResult(stopwatch, "Ours");

            // 初始化统计量
            readerThreadNum = 0;
            writerThreadNum = 0;
            readWaitTime = 0;
            writeWaitTime = 0;
            finishedWorkerCount = 0;
            stopwatch = new Stopwatch();

            // 测试使用来完成读写任务，作为测试的BaseLine;
            stopwatch.Start();
            for (int i = 0; i < totalThreadNum; i++)
            {
                int randNum = randNumList[i];
                // randNum范围是0-19，5%的线程为写者
                if (randNum == 0)
                {
                    writerThreadNum++;
                    new Thread(() => WriterBaseline(this)).Start();
                }
                else
                {
                    readerThreadNum++;
                    new Thread(() => ReaderBaseline(this)).Start();
                }
            }

            finished.WaitOne();
            stopwatch.Stop();
            // 输出测试结果
            PrintTestResult(stopwatch, "Baseline");

            System.Console.WriteLine("\nTest Case 2 end!\n");
        }
    }
}