using System;
using System.Threading;

namespace ReadWriteLock
{
    class ReentrantReaderWriterLock
    {
        private int exclusiveThreadId;                                          // 独占读写锁的线程 ID

        private AutoResetEvent writeEvent;                                      // 用来实现写优先
        private AutoResetEvent readWriteEvent;                                  // 用来实现读写互斥

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
            
            writeEvent.WaitOne(); // 先申请写
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
        }
        
        /**
         * 读者释放读写锁 
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