﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

using CodeJam;
using CodeJam.Services;

using JetBrains.Annotations;

using LinqToDB;

using Rsdn.Janus.DataModel;

namespace Rsdn.Janus
{
	public static class DataHelper
	{
		public static IQueryable<T> GetTable<T>(
			[NotNull] this IDataContext context,
			[NotNull] Expression<Func<T, bool>> predicate)
			where T : class
		{
			Code.NotNull(context, nameof(context));
			return context.GetTable<T>().Where(predicate);
		}

		/// <summary>
		/// Получить перечислитель заданной колонки таблицы.
		/// </summary>
		/// <typeparam name="T">тип к которому привести значение колонки</typeparam>
		/// <param name="table">таблица</param>
		/// <param name="name">имя колонки для получения</param>
		/// <returns>перечислитель колонки таблицы</returns>
		public static IEnumerable<T> GetTableColumn<T>(this DataTable table, string name)
		{
			return
				table
					.Rows
					.Cast<DataRow>()
					.Select(row => (T)row[name]);
		}

		public static IJanusDataContext CreateDBContext([NotNull] this IServiceProvider provider)
		{
			if (provider == null)
				throw new ArgumentNullException(nameof(provider));

			return
				provider
					.GetRequiredService<IJanusDatabaseManager>()
					.CreateDBContext();
		}

		public static IEnumerable<IEnumerable<T>> SplitForInClause<T>(
			[NotNull] this IEnumerable<T> source,
			[NotNull]IServiceProvider provider)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (provider == null) throw new ArgumentNullException(nameof(provider));
			return source.SplitToSeries(provider.MaxInClauseElements());
		}

		public static ITable<IVariable> Vars([NotNull] this IDataContext db)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			return db.GetTable<IVariable>();
		}

		public static IQueryable<IVariable> Vars(
			[NotNull] this IDataContext db,
			[CanBeNull] Expression<Func<IVariable, bool>> predicate)
		{
			return GetTable(db, predicate);
		}

		public static IDBVarsManager DBVars([NotNull] this IServiceProvider provider)
		{
			if (provider == null) throw new ArgumentNullException(nameof(provider));
			return provider.GetRequiredService<IDBVarsManager>();
		}

		public static int MaxInClauseElements([NotNull] this IServiceProvider provider)
		{
			if (provider == null) throw new ArgumentNullException(nameof(provider));
			return
				provider
					.GetRequiredService<IJanusDatabaseManager>()
					.GetCurrentDriver()
					.Formatter
					.MaxInClauseElements;
		}
	}
}