//
// FileSearchCategory.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2012 mkrueger
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
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using System.Collections.Generic;
using MonoDevelop.Core.Instrumentation;
using MonoDevelop.Projects;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide;
using ICSharpCode.NRefactory.TypeSystem;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Ide.NavigateToDialog;
using MonoDevelop.Core.Text;
using Gtk;
using System.Linq;

namespace MonoDevelop.Components.MainToolbar
{
	class FileSearchCategory : SearchCategory
	{
		Widget widget;
		public FileSearchCategory (Widget widget) : base (GettextCatalog.GetString("Files"))
		{
			this.widget = widget;
			this.lastResult = new WorkerResult (widget);
		}

		IEnumerable<ProjectFile> files {
			get {
				HashSet<ProjectFile> list = new HashSet<ProjectFile> ();
				foreach (Document doc in IdeApp.Workbench.Documents) {
					// We only want to check it here if it's not part
					// of the open combine.  Otherwise, it will get
					// checked down below.
					if (doc.Project == null && doc.IsFile)
						yield return new ProjectFile (doc.Name);
				}
				
				var projects = IdeApp.Workspace.GetAllProjects ();

				foreach (Project p in projects) {
					foreach (ProjectFile file in p.Files) {
						if (file.Subtype != Subtype.Directory)
							yield return file;
					}
				}
			}
		}

		WorkerResult lastResult;
		string[] validTags = new [] { "f", "file"};

		public override Task<ISearchDataSource> GetResults (SearchPopupSearchPattern searchPattern, CancellationToken token)
		{
			return Task.Factory.StartNew (delegate {
				if (searchPattern.Tag != null && !validTags.Contains (searchPattern.Tag))
					return null;
				WorkerResult newResult = new WorkerResult (widget);
				newResult.pattern = searchPattern.Pattern;
				newResult.IncludeFiles = true;
				newResult.IncludeTypes = true;
				newResult.IncludeMembers = true;

				string toMatch = searchPattern.Pattern;
				newResult.matcher = StringMatcher.GetMatcher (toMatch, true);
				newResult.FullSearch = true;

				foreach (SearchResult result in AllResults (lastResult, newResult)) {
					if (token.IsCancellationRequested)
						return (ISearchDataSource)newResult.results;
					newResult.results.AddResult (result);
				}
				newResult.results.Sort (new DataItemComparer ());
				lastResult = newResult;
				return (ISearchDataSource)newResult.results;
			}, token);
		}

		IEnumerable<SearchResult> AllResults (WorkerResult lastResult, WorkerResult newResult)
		{
			// Search files
			if (newResult.IncludeFiles) {
				newResult.filteredFiles = new List<ProjectFile> ();
				bool startsWithLastFilter = lastResult != null && lastResult.pattern != null && newResult.pattern.StartsWith (lastResult.pattern) && lastResult.filteredFiles != null;
				IEnumerable<ProjectFile> allFiles = startsWithLastFilter ? lastResult.filteredFiles : files;
				foreach (ProjectFile file in allFiles) {
					SearchResult curResult = newResult.CheckFile (file);
					if (curResult != null) {
						newResult.filteredFiles.Add (file);
						yield return curResult;
					}
				}
			}
		}
		
		class WorkerResult 
		{
			public List<ProjectFile> filteredFiles = null;
			public List<ITypeDefinition> filteredTypes = null;
			public List<IMember> filteredMembers  = null;
			
			public string pattern = null;
			public bool isGotoFilePattern;
			public ResultsDataSource results;
			
			public bool FullSearch;
			
			public bool IncludeFiles, IncludeTypes, IncludeMembers;
			
			public Ambience ambience;
			
			public StringMatcher matcher = null;
			
			public WorkerResult (Widget widget)
			{
				results = new ResultsDataSource (widget);
			}
			
			internal SearchResult CheckFile (ProjectFile file)
			{
				int rank;
				string matchString = System.IO.Path.GetFileName (file.FilePath);
				if (MatchName (matchString, out rank)) 
					return new FileSearchResult (pattern, matchString, rank, file, true);
				
				if (!FullSearch)
					return null;
				matchString = FileSearchResult.GetRelProjectPath (file);
				if (MatchName (matchString, out rank)) 
					return new FileSearchResult (pattern, matchString, rank, file, false);
				
				return null;
			}
			
			internal SearchResult CheckType (ITypeDefinition type)
			{
				int rank;
				if (MatchName (type.Name, out rank))
					return new TypeSearchResult (pattern, type.Name, rank, type, false) { Ambience = ambience };
				if (!FullSearch)
					return null;
				if (MatchName (type.FullName, out rank))
					return new TypeSearchResult (pattern, type.FullName, rank, type, true) { Ambience = ambience };
				return null;
			}
			
			internal SearchResult CheckMember (IMember member)
			{
				int rank;
				bool useDeclaringTypeName = member is IMethod && (((IMethod)member).IsConstructor || ((IMethod)member).IsDestructor);
				string memberName = useDeclaringTypeName ? member.DeclaringType.Name : member.Name;
				if (MatchName (memberName, out rank))
					return new MemberSearchResult (pattern, memberName, rank, member, false) { Ambience = ambience };
				if (!FullSearch)
					return null;
				memberName = useDeclaringTypeName ? member.DeclaringType.FullName : member.FullName;
				if (MatchName (memberName, out rank))
					return new MemberSearchResult (pattern, memberName, rank, member, true) { Ambience = ambience };
				return null;
			}
			
			Dictionary<string, MatchResult> savedMatches = new Dictionary<string, MatchResult> ();
			bool MatchName (string name, out int matchRank)
			{
				if (name == null) {
					matchRank = -1;
					return false;
				}
				MatchResult savedMatch;
				if (!savedMatches.TryGetValue (name, out savedMatch)) {
					bool doesMatch = matcher.CalcMatchRank (name, out matchRank);
					savedMatches[name] = savedMatch = new MatchResult (doesMatch, matchRank);
				}
				
				matchRank = savedMatch.Rank;
				return savedMatch.Match;
			}
		}


		class DataItemComparer : IComparer<SearchResult>
		{
			public int Compare (SearchResult o1, SearchResult o2)
			{
				var r = o2.Rank.CompareTo (o1.Rank);
				if (r == 0)
					r = o1.SearchResultType.CompareTo (o2.SearchResultType);
				if (r == 0)
					return String.CompareOrdinal (o1.MatchedString, o2.MatchedString);
				return r;
			}
		}

		struct MatchResult 
		{
			public bool Match;
			public int Rank;
			
			public MatchResult (bool match, int rank)
			{
				this.Match = match;
				this.Rank = rank;
			}
		}
		
	}
}

