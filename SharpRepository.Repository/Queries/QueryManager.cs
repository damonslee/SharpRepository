﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using SharpRepository.Repository.Caching;
using SharpRepository.Repository.Specifications;

namespace SharpRepository.Repository.Queries
{
    /// <summary>
    /// The QueryManager is the middle man between the repository and the caching strategy.
    /// It receives a query that should be run, checks the cache for valid results to return, and if none are found runs the query and caches the results according to the caching strategy.
    /// It also notifies the caching strategy of CRUD operations in case the caching strategy needs to act as a result of a certain action.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class QueryManager<T, TKey> where T : class
    {
        private readonly ICacheItemCleaner _cacheItemCleaner;
        private readonly ICachingStrategy<T, TKey> _cachingStrategy;

        public QueryManager(ICacheItemCleaner cacheItemCleaner, ICachingStrategy<T, TKey> cachingStrategy)
        {
            CacheUsed = false;
            CacheEnabled = true;
            _cacheItemCleaner = cacheItemCleaner;
            _cachingStrategy = cachingStrategy ?? new NoCachingStrategy<T, TKey>();
        }

        public bool CacheUsed { get; private set; }

        public bool CacheEnabled { get; set; }

        public TResult ExecuteGet<TResult>(Func<TResult> query, Expression<Func<T, TResult>> selector, TKey key)
        {
            TResult result;
            if (CacheEnabled && _cachingStrategy.TryGetResult(key, selector, out result))
            {
                CacheUsed = true;
                return result;
            }

            CacheUsed = false;
            result = query.Invoke();

            //  the cache item converter is basically for EF5, it returns a DynamicProxy for lazy loading purposes
            //  this will go into cache fine but after getting an object from cache, it will error out if you try to update it because it's attached to an old DBContext
            try
            {
                var item = _cacheItemCleaner.CleanItem(result);
                _cachingStrategy.SaveGetResult(key, selector, item);
            }
            catch (Exception)
            {
                // ignore this
                //  this means that the clean up didn't go well, so don't cache it and next time it will be pulled from DB again
            }

            return result;
        }

        public IEnumerable<TResult> ExecuteGetAll<TResult>(Func<IEnumerable<TResult>> query, Expression<Func<T, TResult>> selector, IQueryOptions<T> queryOptions)
        {
            IEnumerable<TResult> result;
            if (CacheEnabled && _cachingStrategy.TryGetAllResult(queryOptions, selector, out result))
            {
                CacheUsed = true;
                return result;
            }

            CacheUsed = false;
            result = query.Invoke();

            //  the cache item converter is basically for EF5, it returns a DynamicProxy for lazy loading purposes
            //  this will go into cache fine but after getting an object from cache, it will error out if you try to update it because it's attached to an old DBContext
            try
            {
                var items = _cacheItemCleaner.CleanItems(result);
                _cachingStrategy.SaveGetAllResult(queryOptions, selector, items);
            }
            catch (Exception)
            {
                // ignore this
                //  this means that the clean up didn't go well, so don't cache it and next time it will be pulled from DB again
            }

            return result;
        }

        public IEnumerable<TResult> ExecuteFindAll<TResult>(Func<IEnumerable<TResult>> query, ISpecification<T> criteria, Expression<Func<T, TResult>> selector,  IQueryOptions<T> queryOptions)
        {
            IEnumerable<TResult> result;
            if (CacheEnabled && _cachingStrategy.TryFindAllResult(criteria, queryOptions, selector, out result))
            {
                CacheUsed = true;
                return result;
            }

            CacheUsed = false;
            result = query.Invoke();

            //  the cache item converter is basically for EF5, it returns a DynamicProxy for lazy loading purposes
            //  this will go into cache fine but after getting an object from cache, it will error out if you try to update it because it's attached to an old DBContext
            try
            {
                var items = _cacheItemCleaner.CleanItems(result);
                _cachingStrategy.SaveFindAllResult(criteria, queryOptions, selector, items);
            }
            catch (Exception)
            {
                // ignore this
                //  this means that the clean up didn't go well, so don't cache it and next time it will be pulled from DB again
            }

            return result;
        }

        public TResult ExecuteFind<TResult>(Func<TResult> query, ISpecification<T> criteria, Expression<Func<T, TResult>> selector,  IQueryOptions<T> queryOptions)
        {
            TResult result;
            if (CacheEnabled && _cachingStrategy.TryFindResult(criteria, queryOptions, selector, out result))
            {
                CacheUsed = true;
                return result;
            }

            CacheUsed = false;
            result = query.Invoke();

            //  the cache item converter is basically for EF5, it returns a DynamicProxy for lazy loading purposes
            //  this will go into cache fine but after getting an object from cache, it will error out if you try to update it because it's attached to an old DBContext
            try
            {
                var item = _cacheItemCleaner.CleanItem(result);
                _cachingStrategy.SaveFindResult(criteria, queryOptions, selector, item);
            }
            catch (Exception)
            {
                // ignore this
                //  this means that the clean up didn't go well, so don't cache it and next time it will be pulled from DB again
            }

            return result;
        }

        public void OnSaveExecuted()
        {
            if (CacheEnabled)
                _cachingStrategy.Save();
        }

        public void OnItemDeleted(TKey key, T item)
        {
            if (CacheEnabled)
                _cachingStrategy.Delete(key, item);
        }

        public void OnItemAdded(TKey key, T item)
        {
            if (CacheEnabled)
                _cachingStrategy.Add(key, item);
        }

        public void OnItemUpdated(TKey key, T item)
        {
            if (CacheEnabled)
                _cachingStrategy.Update(key, item);
        }
    }
}
