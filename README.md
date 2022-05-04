# 读写锁

## 项目简介

### 项目内容

实现一个写优先的读写锁，并且支持可重入功能。

> 写优先：即 Second readers–writers problem 写线程拥有更高的优先级
>
> 可重入： 可重入锁，也叫做递归锁，是指在一个线程中可以多次获取同一把锁，比如：一个线程在执行一个带锁的方法，该方法中又调用了另一个需要相同锁的方法，则该线程可以直接执行调用的方法【即可重入】，而无需重新获得锁

### 约束条件

- 写者与写者互斥
- 写者与读者互斥
- 读者与读者并发
- 不能出现写饥饿
- 支持锁的可重入

## 可重入读写锁原理

### 数据结构

```c#
class ReentrantReaderWriterLock
{
    private int exclusiveThreadId;

    private AutoResetEvent writeEvent;                                    
    private AutoResetEvent readWriteEvent;
    
    private static readonly object _lock = new object();

    private int state;                                                     
    private static readonly object stateLock = new object();               

    private static readonly int SHARED_SHIFT   = 16;                       
    private static readonly int SHARED_UNIT    = (1 << SHARED_SHIFT);       
    private static readonly int MAX_COUNT      = (1 << SHARED_SHIFT) - 1;   
    private static readonly int EXCLUSIVE_MASK = (1 << SHARED_SHIFT) - 1;   
    
    private static int SharedCount(int c)    { return c >> SHARED_SHIFT; } 
    private static int ExclusiveCount(int c) { return c & EXCLUSIVE_MASK; } 
    
    public ReentrantReaderWriterLock();
    public void EnterReadLock();
    public void ExitReadLock();
    public void EnterWriteLock();
    public void ExitWriteLock();
}
```

- exclusiveThreadId：独占当前读写锁的线程ID，非独占时为 -1
- writeEvent： 用来实现写优先
- readWriteEvent：用来实现读写互斥
- _lock：使每次与写者竞争的读者只有一个
- state：为了支持可重入维护的状态量
  - 高16位表示重入的读者数量，低16位表示重入的写者数量
- stateLock：被Monitor用来保护 state 的并发修改

### 实现细节

#### 读者请求读写锁

- 首先获取当前线程 ID与独占读写锁 ID比较，当ID相同时进行重入操作，否则进行竞争
- 读者重入：
  - 对 state变量的高 16位加 1，然后函数结束，读线程不受阻塞
- 读者竞争读写锁：
  - 首先申请 _lock互斥量，保证每次与写者竞争的读者只有一个
  - 申请写信号 writeEvent
  - 第一个读者申请读写信号 readWriteEvent
  - 对 state变量的高 16位加 1
  - 释放写信号 writeEvent
  - 最后释放 _lock互斥量

```c#
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
```

#### 读者释放读写锁

- 首先获取当前线程 ID与独占读写锁 ID比较，当ID相同时进行重入操作，否则进行竞争
- 读者重入释放读写锁：
  - 对 state变量的高 16位减 1，然后函数结束，读线程退出不受阻塞
- 读者正常释放读写锁：
  - 对 state变量的高 16位加 1
  - 最后一个读者释放读写信号 readWriteEvent

```c#
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
```

#### 写者申请读写锁

- 首先获取当前线程 ID与独占读写锁 ID比较，当ID相同时并且没有读重入时进行重入操作，否则进行竞争
- 写者重入：
  - 对 state变量的低 16位加 1，然后函数结束，写线程退出不受阻塞
- 写者竞争读写锁：
  - 首先申请写信号 writeEvent
  - 再申请读写信号 readWriteEvent
  - 对 state变量的高 16位加 1
  - 设置独占读写锁 ID为当前线程 ID

```c#
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
    if (ExclusiveCount(state + 1) > MAX_COUNT)
    {
        throw new Exception("写锁的数量大于65535!");
    }
    exclusiveThreadId = Environment.CurrentManagedThreadId;
    state += 1;
    Monitor.Exit(stateLock);
}
```

#### 写者释放读写锁

- 对 state变量的低 16位减 1
- state不为0进行重入释放操作，否则进行正常释放
- 写者重入释放：
  - 函数直接结束，写线程释放不受阻塞
- 写者正常释放：
  - 首先释放读写信号 readWriteEvent
  - 再释放写信号 writeEvent
  - 设置独占读写锁 ID为 -1

```c#
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
```

## 正确性测试

### 读写锁正确性测试

> 测试代码与官方文档基本一致
>
> https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim

#### 测试条件

- 实现了 SynchronizedCache 一个多线程共享的缓冲区（数据结构为：Key-Value）
- 使用我们自己实现的 ReentrantReaderWriterLock 来保证读者写者正确性。使用 SynchronizedCache 来保证与官方文档测试读写锁接口相同。

#### 测试过程

- 启动写者线程进行写入

  ```c#
  tasks.Add(Task.Run(() =>
  {
      String[] vegetables = { "broccoli", "cauliflower", "carrot", "sorrel", "baby turnip",
          "beet", "brussel sprout", "cabbage", "plantain",
          "spinach", "grape leaves", "lime leaves", "corn",
          "radish", "cucumber", "raddichio", "lima beans" };
      for (int ctr = 1; ctr <= vegetables.Length; ctr++)
          sc.Add(ctr, vegetables[ctr - 1]);
  
      itemsWritten = vegetables.Length;
      Console.WriteLine("Task {0} wrote {1} items\n",
          Task.CurrentId, itemsWritten);
  }));
  ```

- 启动两个读者线程，一个从字典首都读到尾部，一个从字典尾部读到首部

  ```c#
  for (int ctr = 0; ctr <= 1; ctr++) {
      bool desc = ctr == 1;
      tasks.Add(Task.Run( () => { 
          int start, last, step;
          int items;
          do {
              String output = String.Empty;
              items = sc.Count;
              if (! desc) {
                  start = 1;
                  step = 1;
                  last = items;
              }
              else {
                  start = items;
                  step = -1;
                  last = 1;
              }
  
              for (int index = start; desc ? index >= last : index <= last; index += step)
                  output += String.Format("[{0}] ", sc.Read(index));
  
              Console.WriteLine("Task {0} read {1} items: {2}\n",
                  Task.CurrentId, items, output);
          } while (items < itemsWritten | itemsWritten == 0);
      }));
  }
  ```

#### 测试结果

![image-20220430171005120](https://typora-anjt.oss-cn-shanghai.aliyuncs.com/image-20220430171005120.png)

### 可重入性测试

> 写锁可以重入写锁，读锁可以重入写锁，但是写锁不可以重入读锁

#### 测试读锁重入写锁

```c#
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
```

#### 测试写锁重入写锁

```c#
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
```

#### 测试读锁重入读锁

```c#
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
```

#### 测试读锁重入读锁

```c#
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
```

#### 测试结果

![image-20220430190809907](https://typora-anjt.oss-cn-shanghai.aliyuncs.com/image-20220430190809907.png)

## 性能分析

ReaderWriterLockSlim 作为 Baseline做性能比较

测试代码见：<a href="ReadWriteLock/TestCase2.cs">TestCase2.cs</a>

### 测试条件与参数

- 读写线程总数:1024个
- 假设读取时间:10ms，写入时间:100ms
- 写者比例:5%，利用随机数产生器控制读写者比例。并记录随机数，保证两次实验随机数相同。
- 当 1024个线程全部执行结束后输出读者和写者的平均等待时间

### 测试结果

![image-20220430183027223](https://typora-anjt.oss-cn-shanghai.aliyuncs.com/image-20220430183027223.png)

小优

## 优缺点与进一步优化

- 优点

  - 写优先
  - 可重入

- 缺点

  - 没有用自旋操作进一步优化申请锁

  - 不支持锁的升级
