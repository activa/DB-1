#region License
//=============================================================================
// Iridium - Porable .NET ORM 
//
// Copyright (c) 2015-2017 Philippe Leybaert
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//=============================================================================
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Iridium.Reflection;

namespace Iridium.DB
{
    internal abstract class DataSet
    {
        internal virtual IList NewObjects { get; set; }
    }

    internal sealed class DataSet<T> : DataSet, IDataSet<T>
    {
        private readonly Repository<T> _repository;
        private readonly FilterSpec _filter;
        private readonly SortOrderSpec _sortOrder;
        private readonly List<Expression<Func<T, object>>> _relationsToLoad;
        private readonly List<Action<T>> _actions;
        private int? _skip;
        private int? _take;
        private readonly TableSchema.Relation _parentRelation;
        private readonly object _parentObject;
        private List<T> _newObjects;

        public DataSet(Repository repository)
        {
            _repository = (Repository<T>) repository;
        }

        public DataSet(StorageContext context)
        {
            _repository = context.GetRepository<T>();
        }

        internal override IList NewObjects => _newObjects;

        [Preserve]
        public DataSet(Repository repository, FilterSpec filter)
        {
            _repository = (Repository<T>)repository;
            _filter = filter;
        }

        [Preserve]
        public DataSet(Repository repository, FilterSpec filter, TableSchema.Relation parentRelation, object parentObject)
        {
            _repository = (Repository<T>)repository;
            _filter = filter;

            _parentRelation = parentRelation;
            _parentObject = parentObject;
        }

        private DataSet(DataSet<T> baseDataSet, FilterSpec newFilterSpec = null, SortOrderSpec newSortSpec = null, IEnumerable<Expression<Func<T,object>>> additionalRelations = null, IEnumerable<Action<T>> additionalActions = null)
        {
            if (baseDataSet._newObjects != null && baseDataSet._newObjects.Count > 0)
                throw new Exception("DataSet with added objects can't be chained");

            _repository = baseDataSet._repository;

            _skip = baseDataSet._skip;
            _take = baseDataSet._take;

            _filter = newFilterSpec ?? baseDataSet._filter;
            _sortOrder = newSortSpec ?? baseDataSet._sortOrder;

            if (baseDataSet._relationsToLoad != null)
                _relationsToLoad = new List<Expression<Func<T, object>>>(baseDataSet._relationsToLoad);

            if (additionalRelations != null)
            {
                if (_relationsToLoad == null)
                    _relationsToLoad = new List<Expression<Func<T, object>>>(additionalRelations);
                else
                    _relationsToLoad.AddRange(additionalRelations);
            }

            if (additionalActions != null)
            {
                if (_actions == null)
                    _actions = new List<Action<T>>(additionalActions);
                else
                    _actions.AddRange(additionalActions);
            }

            _parentRelation = baseDataSet._parentRelation;
            _parentObject = baseDataSet._parentObject;
        }

        public IAsyncDataSet<T> Async()
        {
            return new AsyncDataSet<T>(this);
        }

        public IDataSet<T> Where(Expression<Func<T, bool>> whereExpression)
        {
            return new DataSet<T>(this, newFilterSpec: new FilterSpec(whereExpression, _filter));
        }

        public IDataSet<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return new DataSet<T>(this, newSortSpec: new SortOrderSpec(keySelector,SortOrder.Ascending, _sortOrder));
        }

        public IDataSet<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return new DataSet<T>(this, newSortSpec: new SortOrderSpec(keySelector, SortOrder.Descending, _sortOrder));
        }

        public IDataSet<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return new DataSet<T>(this, newSortSpec: new SortOrderSpec(keySelector, SortOrder.Ascending, _sortOrder));
        }

        public IDataSet<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return new DataSet<T>(this, newSortSpec: new SortOrderSpec(keySelector, SortOrder.Descending, _sortOrder));
        }

        public IDataSet<T> OrderBy(QueryExpression expression, SortOrder sortOrder)
        {
            return new DataSet<T>(this, newSortSpec: new SortOrderSpec(expression, sortOrder, _sortOrder));
        }

        private IEnumerable<T> Enumerate()
        {
            if (_newObjects != null && _newObjects.Count > 0)
                throw new Exception("Trying to enumerate dataset with pending changes");

            return _repository.List(_repository.CreateQuerySpec(_filter, sortSpec: _sortOrder, skip: _skip, take: _take), _relationsToLoad, _parentRelation, _parentObject, _actions);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Enumerate().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }



        public void Purge()
        {
            _repository.Purge();
        }

        public T Read(object key, params Expression<Func<T, object>>[] relationsToLoad)
        {
            return _repository.Read(key,relationsToLoad);
        }

        public T Read(Expression<Func<T, bool>> condition, params Expression<Func<T, object>>[] relationsToLoad)
        {
            return WithRelations(relationsToLoad).FirstOrDefault(condition);
        }

        public T Load(T obj, object key, params Expression<Func<T, object>>[] relationsToLoad)
        {
            return _repository.Load(obj, key, relationsToLoad);
        }

        public bool Save(T obj, params Expression<Func<T, object>>[] relationsToSave)
        {
            return _repository.Save(obj, create: null, relationsToSave: LambdaRelationFinder.FindRelations(relationsToSave, _repository.Schema));
        }

        public bool Save(IEnumerable<T> objects, params Expression<Func<T, object>>[] relationsToSave)
        {
            return _repository.Context.RunTransaction(() => objects.All(o => Save(o, relationsToSave)));
        }

        public bool InsertOrUpdate(T obj, params Expression<Func<T, object>>[] relationsToSave)
        {
            return _repository.Save(obj, create: null, relationsToSave: LambdaRelationFinder.FindRelations(relationsToSave, _repository.Schema));
        }

        public bool InsertOrUpdate(IEnumerable<T> objects, params Expression<Func<T, object>>[] relationsToSave)
        {
            return _repository.Context.RunTransaction(() => objects.All(o => InsertOrUpdate(o, relationsToSave)));
        }

        public bool Insert(T obj, params Expression<Func<T, object>>[] relationsToSave)
        {
            return Insert(obj, deferSave: null, relationsToSave: relationsToSave);
        }

        public bool Add(T obj)
        {
            return Insert(obj, deferSave: null);
        }

        public bool Insert(T obj, bool? deferSave, params Expression<Func<T,object>>[] relationsToSave)
        {
            var isOneToMany = _parentRelation?.RelationType == TableSchema.RelationType.OneToMany;

            if (isOneToMany)
            {
                var hasParentId = _parentRelation.LocalField.GetField(_parentObject) != _parentRelation.LocalField.FieldType.Inspector().DefaultValue();

                if (deferSave == null)
                {
                    deferSave = !hasParentId;
                }
                else
                {
                    if (!deferSave.Value && !hasParentId)
                        throw new ArgumentException($"{nameof(deferSave)} cannot be false when parent object is not saved yet");
                }

                if (!deferSave.Value)
                {
                    if (_parentRelation.ReverseRelation != null)
                        _parentRelation.ReverseRelation.SetField(obj, _parentObject);

                    _parentRelation.ForeignField.SetField(obj, _parentRelation.LocalField.GetField(_parentObject));
                }
                else
                {
                    if (_newObjects == null)
                        _newObjects = new List<T>();

                    _newObjects.Add(obj);

                    return true;
                }
            }
            else
            {
                if (deferSave == true)
                    throw new ArgumentException($"{nameof(deferSave)} is only applicable for one-to-many relations");
            }

            return _repository.Save(obj, create: true, relationsToSave: LambdaRelationFinder.FindRelations(relationsToSave, _repository.Schema));
        }

        public bool Insert(IEnumerable<T> objects, bool? deferSave = null, params Expression<Func<T, object>>[] relationsToSave)
        {
            return _repository.Context.RunTransaction(() => objects.All(o => Insert(o, deferSave, relationsToSave)));
        }

        public bool Update(T obj, params Expression<Func<T, object>>[] relationsToSave)
        {
            return _repository.Save(obj, create: false, relationsToSave: LambdaRelationFinder.FindRelations(relationsToSave, _repository.Schema));
        }

        public bool Update(IEnumerable<T> objects, params Expression<Func<T, object>>[] relationsToSave)
        {
            return _repository.Context.RunTransaction(() => objects.All(o => Update(o, relationsToSave)));
        }

        public bool Delete(T obj)
        {
            return _repository.Delete(obj);
        }

        public bool Delete(IEnumerable<T> objects)
        {
            return _repository.Context.RunTransaction(() => objects.All(Delete));
        }

        public bool DeleteAll()
        {
            return _repository.Delete(_repository.CreateQuerySpec(_filter));
        }

        public bool Delete(Expression<Func<T, bool>> filter)
        {
            return Where(filter).DeleteAll();
        }

        public bool Delete(QueryExpression filterExpression)
        {
            return _repository.Delete(_repository.CreateQuerySpec(new FilterSpec(filterExpression)));
        }

        public IDataSet<T> WithRelations(params Expression<Func<T, object>>[] relationsToLoad)
        {
            return new DataSet<T>(this, additionalRelations: relationsToLoad);
        }

        public IDataSet<T> Where(QueryExpression filterExpression)
        {
            return new DataSet<T>(this, newFilterSpec: new FilterSpec(filterExpression));
        }

        public IDataSet<T> WithAction(Action<T> action)
        {
            return WithActions(action);
        }

        public IDataSet<T> WithActions(params Action<T>[] actions)
        {
            return new DataSet<T>(this, additionalActions: actions);
        }


        public IDataSet<T> Skip(int n)
        {
            n += _skip ?? 0;

            return new DataSet<T>(this) {_skip = n};
        }

        public IDataSet<T> Take(int n)
        {
            return new DataSet<T>(this) { _take = Math.Min(n,_take ?? int.MaxValue) };
        }

        public T First()
        {
            return _repository.List(_repository.CreateQuerySpec(_filter, sortSpec: _sortOrder, skip: _skip, take: 1), _relationsToLoad, _parentRelation, _parentObject, _actions).First();
        }

        public T First(Expression<Func<T, bool>> filter)
        {
            return _repository.List(_repository.CreateQuerySpec(new FilterSpec(filter, _filter), sortSpec: _sortOrder, skip: _skip, take: 1), _relationsToLoad, _parentRelation, _parentObject, _actions).First();
        }

        public T FirstOrDefault()
        {
            return _repository.List(_repository.CreateQuerySpec(_filter, sortSpec: _sortOrder, skip: _skip, take: 1), _relationsToLoad, _parentRelation, _parentObject, _actions).FirstOrDefault();
        }

        public T FirstOrDefault(Expression<Func<T, bool>> filter)
        {
            return _repository.List(_repository.CreateQuerySpec(new FilterSpec(filter, _filter), sortSpec: _sortOrder, skip: _skip, take: 1), _relationsToLoad, _parentRelation, _parentObject, _actions).FirstOrDefault();
        }

        public long Count()
        {
            return _repository.GetAggregate<long>(Aggregate.Count, _repository.CreateQuerySpec(_filter, skip: _skip, take: _take));
        }

        public long Count(Expression<Func<T, bool>> filter)
        {
            return _repository.GetAggregate<long>(Aggregate.Count, _repository.CreateQuerySpec(new FilterSpec(filter, _filter), skip: _skip, take: _take));
        }

        public TScalar Max<TScalar>(Expression<Func<T, TScalar>> expression, Expression<Func<T, bool>> filter)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Max, _repository.CreateQuerySpec(new FilterSpec(filter, _filter),new ScalarSpec(expression), skip: _skip, take: _take));
        }

        public TScalar Min<TScalar>(Expression<Func<T, TScalar>> expression, Expression<Func<T, bool>> filter)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Min, _repository.CreateQuerySpec(new FilterSpec(filter, _filter), new ScalarSpec(expression), skip: _skip, take: _take));
        }

        public TScalar Sum<TScalar>(Expression<Func<T, TScalar>> expression, Expression<Func<T, bool>> filter)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Sum, _repository.CreateQuerySpec(new FilterSpec(filter, _filter), new ScalarSpec(expression), skip: _skip, take: _take));
        }

        public bool All(Expression<Func<T, bool>> filter)
        {
            return Count(Expression.Lambda<Func<T, bool>>(Expression.Not(filter.Body), filter.Parameters[0])) == 0;
        }

        public TScalar Max<TScalar>(Expression<Func<T, TScalar>> expression)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Max, _repository.CreateQuerySpec(_filter, new ScalarSpec(expression), skip: _skip, take: _take));
        }

        public TScalar Min<TScalar>(Expression<Func<T, TScalar>> expression)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Min, _repository.CreateQuerySpec(_filter, new ScalarSpec(expression), skip: _skip, take: _take));
        }

        public TScalar Sum<TScalar>(Expression<Func<T, TScalar>> expression)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Sum, _repository.CreateQuerySpec(_filter, new ScalarSpec(expression), skip: _skip, take: _take));
        }

        private TScalar Average<TScalar>(LambdaExpression expression)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Average, _repository.CreateQuerySpec(_filter, new ScalarSpec(expression), skip: _skip, take: _take));
        }

        private TScalar Average<TScalar>(LambdaExpression expression, Expression<Func<T, bool>> filter)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Average, _repository.CreateQuerySpec(new FilterSpec(filter, _filter), new ScalarSpec(expression), skip: _skip, take: _take));
        }

        public double Average(Expression<Func<T, int>> expression) => Average<double>(expression);
        public double? Average(Expression<Func<T, int?>> expression) => Average<double?>(expression);
        public double Average(Expression<Func<T, double>> expression) => Average<double>(expression);
        public double? Average(Expression<Func<T, double?>> expression) => Average<double?>(expression);
        public double Average(Expression<Func<T, long>> expression) => Average<double>(expression);
        public double? Average(Expression<Func<T, long?>> expression) => Average<double?>(expression);
        public decimal Average(Expression<Func<T, decimal>> expression) => Average<decimal>(expression);
        public decimal? Average(Expression<Func<T, decimal?>> expression) => Average<decimal?>(expression);

        public double Average(Expression<Func<T, int>> expression, Expression<Func<T, bool>> filter) => Average<double>(expression, filter);
        public double? Average(Expression<Func<T, int?>> expression, Expression<Func<T, bool>> filter) => Average<double?>(expression, filter);
        public double Average(Expression<Func<T, double>> expression, Expression<Func<T, bool>> filter) => Average<double>(expression, filter);
        public double? Average(Expression<Func<T, double?>> expression, Expression<Func<T, bool>> filter) => Average<double?>(expression, filter);
        public double Average(Expression<Func<T, long>> expression, Expression<Func<T, bool>> filter) => Average<double>(expression, filter);
        public double? Average(Expression<Func<T, long?>> expression, Expression<Func<T, bool>> filter) => Average<double?>(expression, filter);
        public decimal Average(Expression<Func<T, decimal>> expression, Expression<Func<T, bool>> filter) => Average<decimal>(expression, filter);
        public decimal? Average(Expression<Func<T, decimal?>> expression, Expression<Func<T, bool>> filter) => Average<decimal?>(expression, filter);

        public TScalar Average<TScalar>(Expression<Func<T, TScalar>> expression)
        {
            return _repository.GetAggregate<TScalar>(Aggregate.Average, _repository.CreateQuerySpec(_filter, new ScalarSpec(expression), skip: _skip, take: _take));
        }

        public bool Any()
        {
            return _repository.GetAggregate<bool>(Aggregate.Any, _repository.CreateQuerySpec(_filter, skip: _skip, take: _take));
        }


        public bool Any(Expression<Func<T, bool>> filter)
        {
            return _repository.GetAggregate<bool>(Aggregate.Any, _repository.CreateQuerySpec(new FilterSpec(filter, _filter), skip: _skip, take: _take));
        }

        public T ElementAt(int index)
        {
            return Skip(index).Take(1).FirstOrDefault();
        }

        public IObjectEvents<T> Events => _repository.Events;
        
    }
}