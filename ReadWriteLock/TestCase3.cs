using System;
using System.Threading.Tasks;

namespace ReadWriteLock
{
    /**
     * 测试可重入读写锁
     * 写锁可以重入写锁，读锁可以重入写锁，写锁不可重入读锁
     */
    public class TestCase3
    {
        public void Test()
        {
            System.Console.WriteLine("\nTest case 3 start!\n");
            ReentrantReaderWriterLock rwLock = new ReentrantReaderWriterLock();
            
            // 测试读锁重入写锁
            Task.Run(() =>
            {
                rwLock.EnterWriteLock();
                rwLock.EnterReadLock();
                rwLock.EnterReadLock();
                System.Console.WriteLine("读锁重入写锁成功");
                rwLock.ExitReadLock();
                rwLock.ExitReadLock();
                rwLock.ExitWriteLock();
            }).Wait();
            
            // 测试写锁重入写锁
            Task.Run(() =>
            {
                rwLock.EnterWriteLock();
                rwLock.EnterWriteLock();
                rwLock.EnterWriteLock();
                System.Console.WriteLine("写锁重入写锁成功");
                rwLock.ExitWriteLock();
                rwLock.ExitWriteLock();
                rwLock.ExitWriteLock();
            }).Wait();
            
            // 测试读锁重入读锁
            Task.Run(() =>
            {
                rwLock.EnterReadLock();
                rwLock.EnterReadLock();
                rwLock.EnterReadLock();
                System.Console.WriteLine("读锁重入读锁成功");
                rwLock.ExitReadLock();
                rwLock.ExitReadLock();
                rwLock.ExitReadLock();
            }).Wait();
            
            // 测试写锁重入读锁
            Task.Run(() =>
            {
                try
                {
                    rwLock.EnterWriteLock();
                    rwLock.EnterReadLock();
                    rwLock.EnterWriteLock();
                    System.Console.WriteLine("写锁重入读锁成功");
                    rwLock.ExitWriteLock();
                    rwLock.ExitReadLock();
                    rwLock.ExitWriteLock();
                }catch (Exception e)
                {
                    System.Console.WriteLine(e.Message);
                    rwLock.ExitReadLock();
                    rwLock.ExitWriteLock();
                }
            }).Wait();
            
            System.Console.WriteLine("\nTest case 3 end!");
        }
    }
}

