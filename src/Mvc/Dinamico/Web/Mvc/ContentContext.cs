﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using N2.Collections;
using N2.Persistence.Finder;
using N2.Web.Mvc.Html;

namespace N2.Web.Mvc
{
	public class TreeBuilder : Tree, IHtmlString
	{
		#region Constructor

		public TreeBuilder(HierarchyBuilder builder)
			: base(builder)
		{
		}

		public TreeBuilder(HierarchyNode<ContentItem> root)
			: base(root)
		{
		}

		#endregion

		#region IHtmlString Members

		public string ToHtmlString()
		{
			return ToString();
		}

		#endregion
	}

	public class LinkBuilder : Link, IHtmlString
	{
		#region Constructor

		public LinkBuilder()
			: base(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)
		{
		}

		public LinkBuilder(string text, string href)
			: base(text, string.Empty, string.Empty, href, string.Empty)
		{
		}

		public LinkBuilder(string text, string title, string target, string href)
			: base(text, title, target, href, string.Empty)
		{
		}

		public LinkBuilder(string text, string title, string target, string href, string className)
			: base(text, title, target, href, className)
		{
		}

		public LinkBuilder(ILink link)
		{
			UpdateFrom(link);
		}

		public LinkBuilder(ContentItem item)
			: base(item, string.Empty)
		{
		}

		public LinkBuilder(ContentItem item, string className)
			: base(item.Title, string.Empty, string.Empty, item.Url, className)
		{
			if (item is ILink)
				UpdateFrom(item as ILink);
		}

		#endregion

		#region IHtmlString Members

		public string ToHtmlString()
		{
			return ToString();
		}

		#endregion
	}

	public class DisplayHelper<TModel> : DynamicObject where TModel : class
	{
		ContentContext<TModel> content;
		bool renderDisplayables;

		public DisplayHelper(ContentContext<TModel> content, bool renderDisplayables)
		{
			this.content = content;
			this.renderDisplayables = renderDisplayables;
		}

		public override IEnumerable<string> GetDynamicMemberNames()
		{
			return content.Current.GetType().GetProperties().Select(p => p.Name);
		}
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			string name = binder.Name;
			if (renderDisplayables)
			{
				object html = content.Html.DisplayContent(name).SwallowExceptions().ToString();

				if (html == null)
					html = content.CurrentItem[name];

				result = html.ToHtmlString();
			}
			else
				result = content.CurrentItem[name];

			return true;

			//var id = RegisterExtensions.GetRegistrationExpression(content).CreateDefinition(Definitions.Static.DefinitionDictionary.Instance);

			//var di = id.Displayables.FirstOrDefault(d => d.Name == binder.Name);

			//if (di == null)
			//{
			//    result = null;
			//    return false;
			//}
			
			//Page p = new Page();
			//di.AddTo(content.CurrentItem, binder.Name, p);

			//using(var sw = new StringWriter())
			//using(var htw = new HtmlTextWriter(sw))
			//{
			//    p.RenderControl(htw);
			//    result = sw.ToHtmlString();
			//    return true;
			//}
		}
	}
	
	public class ContentContext<TModel> where TModel : class
	{
		ContentWebViewPage<TModel> page;

		public ContentContext(ContentWebViewPage<TModel> page)
		{
			this.page = page;
		}

		public HtmlHelper<TModel> Html
		{
			get { return page.Html; }
		}

		public TModel Current
		{
			get { return CurrentItem as TModel; }
		}

		internal ContentItem CurrentItem
		{
			get { return page.Html.ViewContext.CurrentItem(); }
		}

		public ContentItem StartPage
		{
			get { return N2.Find.StartPage; }
		}

		public ContentItem RootPage
		{
			get { return N2.Find.RootItem; }
		}

		protected virtual ItemFilter DefaultFilter
		{
			get { return new NavigationFilter(); }
		}

		public IEnumerable<ContentItem> Ancestors(ContentItem item = null, ItemFilter filter = null)
		{
			return (filter ?? DefaultFilter).Pipe(N2.Find.EnumerateParents(item ?? CurrentItem, StartPage, true));
		}

		public IEnumerable<ContentItem> AncestorsBetween(int skipLevel = 0, int takeLevels = 1)
		{
			return N2.Find.EnumerateParents(CurrentItem, StartPage, true).Reverse().Skip(skipLevel).Take(takeLevels);
		}

		public IEnumerable<ContentItem> Children(ContentItem item, ItemFilter filter = null)
		{
			return item.GetChildren(filter ?? new NavigationFilter());
		}

		public IEnumerable<ContentItem> Descendants(ContentItem item, ItemFilter filter = null)
		{
			return N2.Find.EnumerateChildren(item).Where((filter ?? DefaultFilter).Match);
		}

		public IEnumerable<ContentItem> Siblings(ContentItem item = null)
		{
			if (item == null)
				item = CurrentItem;
			if (item.Parent == null)
				return Enumerable.Empty<ContentItem>();

			return item.Parent.GetChildren(new NavigationFilter());
		}

		public int Level(ContentItem item = null)
		{
			return Ancestors(item).Count();
		}

		public ContentItem AncestorAtLevel(int level)
		{
			return Ancestors().Reverse().Skip(level).FirstOrDefault();
		}

		public Tree TreeFrom(int skipLevels = 0, int takeLevels = 3, bool rootless = false, Func<ContentItem, string> cssGetter = null, ItemFilter filter = null)
		{
			return TreeFrom(AncestorAtLevel(skipLevels), takeLevels, rootless, cssGetter, filter);
		}

		public Tree TreeFrom(ContentItem item, int takeLevels = 3, bool rootless = false, Func<ContentItem, string> cssGetter = null, ItemFilter filter = null)
		{
			if (cssGetter == null)
				cssGetter = GetNavigationClass;

			return new TreeBuilder(new TreeHierarchyBuilder(item, takeLevels))
				.ExcludeRoot(rootless)
				.LinkProvider((i) => LinkTo(i).Class(cssGetter(i)))
				.Filters(filter ?? DefaultFilter);
		}

		public string GetNavigationClass(ContentItem item)
		{
			return Current == item ? "current" : Ancestors().Contains(item) ? "trail" : "";
		}

		public ILinkBuilder LinkTo(ContentItem item)
		{
			return new LinkBuilder(item);
		}

		public IItemFinder Find()
		{
			return Html.ResolveService<IItemFinder>();
		}

		public IQueryAction FindDescendant(ContentItem root = null)
		{
			if (root == null)
				root = CurrentItem;
			return Html.ResolveService<IItemFinder>().Where.AncestralTrail.Like(Utility.GetTrail(root) + "%");
		}

		public dynamic Display
		{
			get { return new DisplayHelper<TModel>(this, true); }
		}

		public dynamic Data
		{
			get { return new DisplayHelper<TModel>(this, false); }
		}
	}
}