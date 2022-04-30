using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReadWriteLock;

/**
 * 测试读写锁的写互斥
 * 申请100个线程，每个线程对变量自增100次
 * 检测变量最终是否为10000
 */
public class TestCase2
{
    private int num = 0;
    private ReentrantReaderWriterLock rwLock = new ReentrantReaderWriterLock();
    
    public void Test()
    {
        System.Console.WriteLine("\nTest case 2 start!\n");
        var tasks = new List<Task>();
        for (int ctr = 0; ctr < 100; ctr++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    rwLock.EnterWriteLock();
                    this.num += 1;
                    rwLock.ExitWriteLock();
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        System.Console.WriteLine("num: {0}", this.num);
        System.Console.WriteLine("\nTest case 2 end!");
    }
}