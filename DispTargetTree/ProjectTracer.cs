using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.Win32;
using System.IO;

namespace DispTargetTree
{
	class ProjectTracer
	{
		public Project Project { get; private set; }
		public ProjectInstance ProjectInstance { get; private set; }
		public ProjectTracer( string projPath )
		{
			var col = ProjectCollection.GlobalProjectCollection;
			Project = col.LoadProject( projPath ); //new Project( projPath, );
			ProjectInstance = new ProjectInstance( Project.Xml );
		}
		internal List<string> ResolveProperties( string value )
		{
			//	まず;でパースする
			var list = value.Split( ';' );
			//	次に $( で始まって ) で終わるデータをコンバートする
			var result = new List<string>();
			foreach( var val in list )
			{
				var str = val.Trim();
				//	コンバートしてリストに突っ込む
				if( str.StartsWith( "$(" ) && str.EndsWith( ")" ) )
				{
					str = str.Substring( 2, str.Length-(2+1) ); //	最初の２文字と最後の１文字をカット
					str = ProjectInstance.GetPropertyValue( str );
					var subList = ResolveProperties( str );
					result.AddRange( subList );
				}
				else if( !string.IsNullOrWhiteSpace( str ) )
				{
					result.Add( str );
				}
			}
			return result;
		}
		internal ProjectTargetElement GetTargetElement( ProjectTargetInstance value )
		{
			var importTarget = Project.Imports.Where( im => im.ImportedProject.FullPath == value.FullPath ).First();
			return importTarget.ImportedProject.Targets.Where( t => t.Name == value.Name ).First();
		}
		internal static void SetupToolset()
		{
			Console.WriteLine( $"DefaultToolsVersion={ProjectCollection.GlobalProjectCollection.DefaultToolsVersion}" );
			//	VSは32bitキーしかセットアップしないので、とりあえず３２ビットキーを見る
			bool setToV15 = false;
			RegistryKey keyVs7 = null;
			try
			{
				keyVs7 = Registry.LocalMachine.OpenSubKey( @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\SxS\VS7", false );
			}
			catch( Exception )
			{
				try
				{
					keyVs7 = Registry.LocalMachine.OpenSubKey( @"SOFTWARE\Microsoft\VisualStudio\SxS\VS7", false );
				}
				catch( Exception )
				{
				}
			}
			if( keyVs7 != null )
			{
				try
				{
					var path = keyVs7.GetValue( "15.0" ) as string;
					var toolsetPath = Path.Combine( path, @"MSBuild\15.0\Bin" );
					if( Directory.Exists( toolsetPath ) )
					{
						ProjectCollection.GlobalProjectCollection.AddToolset( new Toolset( "15.0", toolsetPath, ProjectCollection.GlobalProjectCollection, toolsetPath ) );
						ProjectCollection.GlobalProjectCollection.DefaultToolsVersion = "15.0";
						setToV15 = true;
					}
				}
				catch( Exception )
				{
				}
			}
			//	最新バージョンをセットしていない場合は、リストの一番新しいやつを探す
			if( !setToV15 )
			{
				string targetToolset = "";
				Version prevVer = new Version( ProjectCollection.GlobalProjectCollection.DefaultToolsVersion );
				foreach( var toolset in ProjectCollection.GlobalProjectCollection.Toolsets )
				{
					var toolsetVer = new Version( toolset.ToolsVersion );
					if( prevVer < toolsetVer )
					{
						targetToolset = toolset.ToolsVersion;
						prevVer = toolsetVer;
					}
				}
				if( !string.IsNullOrWhiteSpace( targetToolset ) )
				{
					ProjectCollection.GlobalProjectCollection.DefaultToolsVersion = targetToolset;
				}
			}
			Console.WriteLine( $"DefaultToolsVersion={ProjectCollection.GlobalProjectCollection.DefaultToolsVersion}" );
		}
		internal static void DispDefaultCollection()
		{
			foreach( var toolset in ProjectCollection.GlobalProjectCollection.Toolsets )
			{
				Console.WriteLine( $"ToolsVersion={toolset.ToolsVersion}" );
				Console.WriteLine( $"ToolsVersion={toolset.ToolsPath}" );
				if( toolset.Properties.Count != 0 )
				{
					Console.WriteLine( "--Properties--" );
					foreach( var propPair in toolset.Properties )
					{
						Console.WriteLine( $"  {propPair.Key}=[{propPair.Value.EvaluatedValue}]");
					}
				}
				if( toolset.SubToolsets.Count != 0 )
				{
					Console.WriteLine( "--SubToolset--" );
					foreach( var subSet in toolset.SubToolsets )
					{
						Console.WriteLine( $"  SubToolsetVersion={subSet.Value.SubToolsetVersion}" );
						Console.WriteLine(  "  --Properties--" );
						foreach( var propPair in subSet.Value.Properties )
						{
							Console.WriteLine( $"    {propPair.Key}=[{propPair.Value.EvaluatedValue}]" );
						}
					}
				}
				Console.WriteLine("---");
			}
		}
	}
}
