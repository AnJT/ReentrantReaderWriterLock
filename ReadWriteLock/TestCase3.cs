using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ReadWriteLock;
/**
 * 可重入读写锁性能测试
 * ReaderWriterLockSlim作为 Baseline做性能比较
 *
 * 测试条件和参数：
 * 读写线程总数：1024个
 * 假设读取时间：10ms,写入时间：100ms
 * 写者比例：5%，利用随机数惨胜七控制读写者比例，并记录随机数，保证两次实验随机数相同
 * 当 1024个线程全部执行结束后输出读者和写着的平均等待时间
 *
 * 测试结果：
 * Ours所耗总时间7276ms
 * Ours读者等待时间：89965ms，Ours写者等待时间5540ms
 * Ours读者平均等待时间：92ms，Ours写者平均等待时间102ms
 * Baseline所耗总时间7351ms
 * Baseline读者等待时间：107123ms，Baseline写者等待时间7411ms
 * Baseline读者平均等待时间：110ms，Baseline写者平均等待时间137ms
 *
 * 小优
 */
public class TestCase3
{
    private long readWaitTime;
    private long writeWaitTime;

    private int readerThreadNum;
    private int writerThreadNum;
    private int totalThreadNum;

    private ReentrantReaderWriterLock rwLock;

    private ReaderWriterLockSlim rwLockSlim;

    public TestCase3()
    {
        readWaitTime = 0;
        writeWaitTime = 0;

        readerThreadNum = 0;
        writerThreadNum = 0;
        totalThreadNum = 1024;
        
        rwLock = new ReentrantReaderWriterLock();

        rwLockSlim = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
    }

    private static void Reader(TestCase3 testCase)
    {
        Stopwatch stopwatch = new Stopwatch();
        // 记录获取读锁的等待时间
        stopwatch.Start();
        testCase.rwLock.EnterReadLock();
        stopwatch.Stop();
        // 原子操作，更新读者等待总时间
        Interlocked.Add(ref testCase.readWaitTime, stopwatch.ElapsedMilliseconds);
        stopwatch.Reset();
        // 模仿读操作用时
        Thread.Sleep(10);
        // 释放读锁
        testCase.rwLock.ExitReadLock();
    }

    private static void Writer(TestCase3 testCase)
    {
        Stopwatch stopwatch = new Stopwatch();
        // 记录获取写锁的等待时间
        stopwatch.Start();
        testCase.rwLock.EnterWriteLock();
        stopwatch.Stop();
        // 原子操作，更新写者等待总时间
        Interlocked.Add(ref testCase.writeWaitTime, stopwatch.ElapsedMilliseconds);
        stopwatch.Reset();
        // 模仿写操作用时
        Thread.Sleep(100);
        // 释放写锁
        testCase.rwLock.ExitWriteLock();
    }
    
    private static void ReaderBaseline(TestCase3 testCase)
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
    }
    
    private static void WriterBaseline(TestCase3 testCase)
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
    }
    
    private void PrintTestResult(Stopwatch stopwatch, String lockName)
    {
        Console.WriteLine(lockName + "所耗总时间{0}ms", stopwatch.ElapsedMilliseconds);
        Console.WriteLine(lockName + "读者等待时间：{0}ms，" + lockName + "写者等待时间{1}ms", readWaitTime, writeWaitTime);
        Console.WriteLine(lockName + "读者平均等待时间：{0}ms，" + lockName + "写者平均等待时间{1}ms", readWaitTime / readerThreadNum, writeWaitTime / writerThreadNum);
    }

    public void Test()
    {
        System.Console.WriteLine("\nTest Case 3 start!\n");
        
        // 使用stopwatch计算耗时
        Stopwatch stopwatch = new Stopwatch();
        var rand = new Random();
        int[] randNumList = new int[totalThreadNum];
        // 使用随机数，模拟5%的线程为写者线程，同时记录随机数以保证两次测试的公平性
        for (int i = 0; i < totalThreadNum; i++)
            randNumList[i] = rand.Next(20);
        var tasks = new List<Task>();
        // 使用我们自己实现的 ReentrantReaderWriterLock 进行测试
        stopwatch.Start();
        for (int i = 0; i < totalThreadNum; i++)
        {
            int randNum = randNumList[i];
            // randNum范围是0-19，5%的线程为写者
            if (randNum == 0)
            {
                writerThreadNum++;
                tasks.Add(Task.Run(() =>
                {
                    Writer(this);
                }));
            }
            else
            {
                readerThreadNum++;
                tasks.Add(Task.Run(() =>
                {
                    Reader(this);
                }));
            }
        }
        Task.WaitAll(tasks.ToArray());
        stopwatch.Stop();
        // 输出测试结果
        PrintTestResult(stopwatch, "Ours");
        
        // 初始化统计量
        readerThreadNum = 0;
        writerThreadNum = 0;
        readWaitTime = 0;
        writeWaitTime = 0;
        stopwatch = new Stopwatch();
        tasks.Clear();
        // 测试使用来完成读写任务，作为测试的BaseLine;
        stopwatch.Start();
        for (int i = 0; i < totalThreadNum; i++)
        {
            int randNum = randNumList[i];
            // randNum范围是0-19，5%的线程为写者
            if (randNum == 0)
            {
                writerThreadNum++;
                tasks.Add(Task.Run(() =>
                {
                    WriterBaseline(this);
                }));
            }
            else
            {
                readerThreadNum++;
                tasks.Add(Task.Run(() =>
                {
                    ReaderBaseline(this);
                }));
            }
        }
        Task.WaitAll(tasks.ToArray());
        stopwatch.Stop();
        // 输出测试结果
        PrintTestResult(stopwatch, "Baseline");
        
        System.Console.WriteLine("\nTest Case 3 end!\n");
    }
}