using System;
using System.Threading;

namespace ReadWriteLock
{
    /**
     * 可重入读写锁
     * 符合第二读写者问题
     * 支持最多 65535个重入写锁，最多65535个重入读锁
     * 满足以下约束条件：
     * - 写者与写者互斥，写者与读者互斥，读者与读者并发
     * - 写优先
     * - 支持锁的重入，写锁可以重入写锁，读锁可以重入写锁
     */
    class ReentrantReaderWriterLock
    {
        private int exclusiveThreadId;                                          // 独占读写锁的线程 ID

        private AutoResetEvent writeEvent;                                      // 用来实现写优先
        private AutoResetEvent readWriteEvent;                                  // 用来实现读写互斥
        
        private static readonly object _lock = new object();                    // 使每次与写者竞争的读者只有一个

        private int state;                                                      // 高16位表示写者数量，低16位表示读者数量
        private static readonly object stateLock = new object();                // 保护 state变量

        private static readonly int SHARED_SHIFT   = 16;                        // 读者数量在高 16位，自加前先左移
        private static readonly int SHARED_UNIT    = (1 << SHARED_SHIFT);       // 读者自增的单位
        private static readonly int MAX_COUNT      = (1 << SHARED_SHIFT) - 1;   // 支持的最大重入数
        private static readonly int EXCLUSIVE_MASK = (1 << SHARED_SHIFT) - 1;   // 用来获取写者数量的掩码
        private static int SharedCount(int c)    { return c >> SHARED_SHIFT; }  // 获取读者数量
        private static int ExclusiveCount(int c) { return c & EXCLUSIVE_MASK; } // 获取写者数量

        /**
         * 非公平可重入锁构造函数 
         */
        public ReentrantReaderWriterLock()
        {
            state = 0;
            exclusiveThreadId = -1;
            writeEvent     = new AutoResetEvent(true);
            readWriteEvent = new AutoResetEvent(true);
        }
        
        /**
         * 读者请求读写锁
         * 
         * 首先获取当前线程 ID与独占读写锁 ID比较，当ID相同时进行重入操作，否则进行竞争
         * 
         * 读者重入：
         * 对 state变量的高 16位加 1，然后函数结束，读线程不受阻塞
         *
         * 读者竞争读写锁：
         * 首先申请 _lock互斥量，保证每次与写者竞争的读者只有一个
         * 首先申请写信号 writeEvent
         * 第一个读者申请读写信号 readWriteEvent
         * 对 state变量的高 16位加 1
         * 释放写信号 writeEvent
         * 最后释放 _lock互斥量
         */
        public void EnterReadLock()
        {
            Monitor.Enter(stateLock);
            if (Environment.CurrentManagedThreadId == exclusiveThreadId)
            {
                if (SharedCount(state + SHARED_UNIT) > MAX_COUNT)
                {
                    throw new Exception("读锁的数量大于65535!");
                }
                // Reentrant
                state += SHARED_UNIT;
                Monitor.Exit(stateLock);
                return;
            }
            Monitor.Exit(stateLock);
            
            Monitor.Enter(_lock);
            writeEvent.WaitOne();
            Monitor.Enter(stateLock);
            if (SharedCount(state) == 0)
            {
                readWriteEvent.WaitOne();
            }
            if (SharedCount(state + SHARED_UNIT) > MAX_COUNT)
            {
                throw new Exception("读锁的数量大于65535!");
            }
            state += SHARED_UNIT;
            Monitor.Exit(stateLock);
            writeEvent.Set();
            Monitor.Exit(_lock);
        }
        
        /**
         * 读者释放读写锁
         *
         * 首先获取当前线程 ID与独占读写锁 ID比较，当ID相同时进行重入操作，否则进行竞争
         *
         * 读者重入释放读写锁：
         * 对 state变量的高 16位减 1，然后函数结束，读线程退出不受阻塞
         *
         * 读者正常释放读写锁：
         * 对 state变量的高 16位减 1
         * 最后一个读者释放读写信号 readWriteEvent
         */
        public void ExitReadLock()
        {
            Monitor.Enter(stateLock);
            if (Environment.CurrentManagedThreadId == exclusiveThreadId)
            {
                // Reentrant
                state -= SHARED_UNIT;
                Monitor.Exit(stateLock);
                return;
            }
            state -= SHARED_UNIT;
            if (SharedCount(state) != 0){
                Monitor.Exit(stateLock);
                return;
            }
            readWriteEvent.Set();
            Monitor.Exit(stateLock);
        }
        
        /**
         * 写者申请读写锁
         *
         * 首先获取当前线程 ID与独占读写锁 ID比较，当ID相同时并且没有读重入时进行重入操作，否则进行竞争
         *
         * 写者重入：
         * 对 state变量的低 16位加 1，然后函数结束，写线程退出不受阻塞
         *
         * 写者竞争读写锁：
         * 首先申请写信号 writeEvent
         * 再申请读写信号 readWriteEvent
         * 对 state变量的高 16位加 1
         * 设置独占读写锁 ID为当前线程 ID
         */
        public void EnterWriteLock()
        {
            Monitor.Enter(stateLock);
            if (Environment.CurrentManagedThreadId == exclusiveThreadId)
            {
                if (SharedCount(state) != 0)
                {
                    throw new Exception("写锁不可重入读锁!");
                }
                if (ExclusiveCount(state + 1) > MAX_COUNT)
                {
                    throw new Exception("写锁的数量大于65535!");
                }
                // Reentrant
                state += 1;
                Monitor.Exit(stateLock);
                return;
            }
            Monitor.Exit(stateLock);

            writeEvent.WaitOne();
            readWriteEvent.WaitOne();
            Monitor.Enter(stateLock);
            exclusiveThreadId = Environment.CurrentManagedThreadId;
            state += 1;
            Monitor.Exit(stateLock);
        }
        
        /**
         * 写者释放读写锁
         *
         * 对 state变量的低 16位减 1
         * state不为0进行重入释放操作，否则进行正常释放
         *
         * 写者重入释放：
         * 函数直接结束，写线程释放不受阻塞
         *
         * 写者正常释放：
         * 首先释放读写信号 readWriteEvent
         * 再释放写信号 writeEvent
         * 设置独占读写锁 ID为 -1
         */
        public void ExitWriteLock()
        {
            Monitor.Enter(stateLock);
            state -= 1;
            if (ExclusiveCount(state) != 0)
            {
                // Reentrant
                Monitor.Exit(stateLock);
                return;
            }
            exclusiveThreadId = -1;
            Monitor.Exit(stateLock);

            readWriteEvent.Set();
            writeEvent.Set();
        }
    }
}