﻿using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using CodeJam;
using CodeJam.Extensibility;
using CodeJam.Services;

using JetBrains.Annotations;

namespace Rsdn.Janus
{
	[Service(typeof(INavigationPageService))]
	public class NavigationPageService : INavigationPageService, IDisposable
	{
		private readonly INavigationPageFactoryService _navigationPageFactory;
		private readonly Subject<INavigationPage> _currentPageChanged = new Subject<INavigationPage>();
		private readonly Subject<PagesChangedEventArgs> _openedPagesChanged =
			new Subject<PagesChangedEventArgs>();
		private readonly Dictionary<INavigationPage, IDisposable> _openedPagesWithSubscriptions =
			new Dictionary<INavigationPage, IDisposable>();
		private INavigationPage _currentPage;

		public NavigationPageService([NotNull] IServiceProvider serviceProvider)
		{
			if (serviceProvider == null)
				throw new ArgumentNullException(nameof(serviceProvider));

			_navigationPageFactory = serviceProvider.GetRequiredService<INavigationPageFactoryService>();
		}

		#region Implementation of INavigationPageService

		public INavigationPage CurrentPage
		{
			get { return _currentPage; }
			set
			{
				if (_currentPage == value)
					return;

				if (value != null && !_openedPagesWithSubscriptions.ContainsKey(value))
					throw new ArgumentException("Page not found.", nameof(value));

				_currentPage = value;

				_currentPageChanged.OnNext(_currentPage);
			}
		}

		public IObservable<INavigationPage> CurrentPageChanged => _currentPageChanged;

		public ICollection<INavigationPage> OpenedPages => _openedPagesWithSubscriptions.Keys;

		public IObservable<PagesChangedEventArgs> OpenedPagesChanged => _openedPagesChanged;

		public void ShowPage(
			IServiceProvider serviceProvider,
			string name,
			NavigationPageState state,
			bool replaceCurrentTab)
		{
			if (serviceProvider == null)
				throw new ArgumentNullException(nameof(serviceProvider));
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			INavigationPage oldPage = null;
			var newPage = _navigationPageFactory.CreatePage(serviceProvider, name, state);

			if (replaceCurrentTab && CurrentPage != null)
			{
				oldPage = CurrentPage;
				RemovePage(oldPage);
			}

			AddPage(newPage);

			_openedPagesChanged.OnNext(
				new PagesChangedEventArgs(
					oldPage,
					newPage,
					oldPage != null ? PagesChangeType.Updated : PagesChangeType.Added));

			CurrentPage = newPage;
		}

		public bool CanShowPage(IServiceProvider serviceProvider, string name)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return _navigationPageFactory.CanCreatePage(serviceProvider, name);
		}

		public void ReloadPage(IServiceProvider serviceProvider, INavigationPage page)
		{
			if (page == null)
				throw new ArgumentNullException(nameof(page));
			if (!_openedPagesWithSubscriptions.ContainsKey(page))
				throw new ArgumentException("Страница не найдена.", nameof(page));

			var newPage = _navigationPageFactory.CreatePage(serviceProvider, page.Name, page.State);

			RemovePage(page);
			AddPage(newPage);

			_openedPagesChanged.OnNext(new PagesChangedEventArgs(page, newPage, PagesChangeType.Updated));

			if (CurrentPage == page)
				CurrentPage = newPage;
		}

		#endregion

		#region Implementation of IDisposable

		public void Dispose()
		{
			OpenedPages.DisposeAll();
			_currentPageChanged.OnCompleted();
			_openedPagesChanged.OnCompleted();
		}

		#endregion

		#region Private Members

		private void AddPage(INavigationPage page)
		{
			_openedPagesWithSubscriptions.Add(
				page,
				page.Disposed.Subscribe(
					arg =>
					{
						RemovePage(page);
						
						_openedPagesChanged.OnNext(
							new PagesChangedEventArgs(page, null, PagesChangeType.Removed));
						
						if (CurrentPage == page)
							CurrentPage = null; //ToDo: выбирать другю вкладку, если активная была закрыта
					}));
		}

		private void RemovePage(INavigationPage page)
		{
			_openedPagesWithSubscriptions[page].Dispose();
			_openedPagesWithSubscriptions.Remove(page);
		}

		#endregion
	}
}