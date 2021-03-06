﻿/*****************************************************************************************************
 * 本代码版权归@wenli所有，All Rights Reserved (C) 2015-2016
 *****************************************************************************************************
 * CLR版本：4.0.30319.42000
 * 唯一标识：2214b55d-4767-4e7d-a048-73cb5d2cbb9b
 * 机器名称：WENLI-PC
 * 联系人邮箱：wenguoli_520@qq.com
 *****************************************************************************************************
 * 项目名称：$projectname$
 * 命名空间：Wenli.Drive.Redis
 * 类名称：SERedisConnectPool
 * 创建时间：2016/11/8 19:41:28
 * 创建人：wenli
 * 创建说明：
 *****************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using StackExchange.Redis;

namespace Wenli.Drive.Redis.Core
{
    /// <summary>
    ///     redis连接池
    /// </summary>
    internal class SERedisConnectPool : IDisposable
    {
        private int _curConnectionPos;
        private readonly bool _isDisposed = false;
        private List<ConnectionMultiplexer> _pool = new List<ConnectionMultiplexer>();

        public SERedisConnectPool(string connectionStr, int poolSize)
        {
            for (var i = 0; i < poolSize; i++)
                _pool.Add(ConnectionMultiplexer.Connect(connectionStr));
        }

        public SERedisConnectPool(IList<ConnectionMultiplexer> connections)
        {
            if (connections == null)
                throw new ArgumentNullException("SERedisConnectPool.ctor.connections");

            _pool.AddRange(connections);
        }

        public int PoolSize
        {
            get { return _pool.Count; }
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_isDisposed)
                    return;

                foreach (var cnn in _pool)
                    cnn.Dispose();
                _pool = new List<ConnectionMultiplexer>();
            }
        }

        /// <summary>
        ///     从连接池中取出一个连接
        /// </summary>
        /// <returns></returns>
        public ConnectionMultiplexer GetConnection()
        {
            if (_isDisposed)
                throw new Exception("这个池子已经被销毁了,请重新创建池子");

            var index = GetNextPos();
            var cnn = _pool[index];
            return cnn.IsConnected ? cnn : FixConnection(index);
        }

        /// <summary>
        ///     修复pool中特定位置的连接
        /// </summary>
        /// <param name="index">有问题的connection位置</param>
        /// <returns></returns>
        private ConnectionMultiplexer FixConnection(int index)
        {
            lock (this)
            {
                // 从指定位置取出，判断是否被其它线程修复好了
                var cnn = _pool[index];
                if (cnn.IsConnected)
                    return cnn;

                var old = cnn;
                var config = old.Configuration;
                cnn = ConnectionMultiplexer.Connect(config);
                _pool[index] = cnn;
                old.Dispose();

                return cnn;
            }
        }

        /// <summary>
        ///     获得下一个连接的位置
        /// </summary>
        /// <returns></returns>
        private int GetNextPos()
        {
            // 优化逻辑，只有一个连接的情况下，直接返回第一个,减少不必要的运算
            if (PoolSize == 1)
                return 0;

            Interlocked.Add(ref _curConnectionPos, 1);
            var next = _curConnectionPos%PoolSize;
            return Math.Abs(next);
        }
    }
}