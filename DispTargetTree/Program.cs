using Microsoft.Build.Construction;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DispTargetTree
{
	class ExecTargetInfo
	{
		public ExecTargetInfo()
		{
			Name = "";
			FileName = "";
			Parent = null;
			NestLevel = -1;
			Children = new List<ExecTargetInfo>();
		}
		public ExecTargetInfo( ExecTargetInfo parent, string name, string fileName ) : this()
		{
			NestLevel = parent.NestLevel + 1;
			Parent = parent;
			Name = name;
			FileName = fileName;
			parent.Children.Add( this );
		}
		public string Name { get; set; }
		public int NestLevel { get; set; }
		public string FileName { get; set; }
		public override string ToString()
		{
			var indent = new string( ' ', NestLevel );
			if( string.IsNullOrWhiteSpace( FileName ) )
			{
				return $"{indent}{Name}";
			}
			else
			{
				return $"{indent}{Name}*{FileName}";
			}
		}
		public ExecTargetInfo Parent { get; set; }
		public List<ExecTargetInfo> Children { get; private set; }
	}
	class Program
	{
		static void Main( string[] args )
		{
			ProjectTracer.SetupToolset();
			if( args.Length < 1 )
			{
				//	デフォルトのToolset を表示する
				ProjectTracer.DispDefaultCollection();
				return;
			}

			foreach( var arg in args )
			{
				var tracer = new ProjectTracer( arg );
				Console.WriteLine( arg );
				Trace.WriteLine( arg );
				Console.WriteLine( "---Imports---" );
				Trace.WriteLine( "---Imports---" );
				foreach( var import in tracer.Project.Imports )
				{
					Console.WriteLine( import.ImportedProject.FullPath );
					Trace.WriteLine( import.ImportedProject.FullPath );
				}
				Console.WriteLine( "---Properties---" );
				Trace.WriteLine( "---Properties---" );
				foreach( var prop in tracer.ProjectInstance.Properties )
				{
					Console.WriteLine( $"{prop.Name}=[{prop.EvaluatedValue}]");
					Trace.WriteLine( $"{prop.Name}=[{prop.EvaluatedValue}]");
				}
				//	呼び出しターゲットを階層構造で作り上げる
				var root = new ExecTargetInfo();
				//	トップレベルなので、エンプティでいい
				root.Name = "Root";
				root.NestLevel = -1;
				root.Parent = null;
				var initialTargets = new ExecTargetInfo( root, "InitialTargets", "" );
				var defaultTargets = new ExecTargetInfo( root, "DefaultTargets", "" );
				//	まずは、InitialTargets の一覧から呼び出し順を作り上げる
				foreach( var target in tracer.ProjectInstance.InitialTargets )
				{
					AddNewTargets( root, tracer, initialTargets, target );
				}
				//	次にビルドターゲットをたどって作り上げる
				foreach( var target in tracer.ProjectInstance.DefaultTargets )
				{
					AddNewTargets( root, tracer, defaultTargets, target );
				}
				foreach( var target in tracer.Project.Targets )
				{
					if( target.Value.AfterTargetsLocation != null || target.Value.BeforeTargetsLocation != null )
					{
						InsertNonSettingTargets( root, tracer, tracer.GetTargetElement( target.Value ) );
					}
				}
				//	ターゲットをドリルダウンしながら表示する
				var calledTargets = new Dictionary<string, int>();
				foreach( var target in root.Children )
				{
					DispTarget( target, calledTargets );
				}
			}
		}
		private static void DispTarget( ExecTargetInfo target, Dictionary<string, int> calledTargets )
		{
			int callCount;
			if( calledTargets.TryGetValue( target.Name, out callCount ) == false )
			{
				callCount = 0;
			}
			callCount++;
			calledTargets[target.Name] = callCount; ;
			Console.WriteLine( $"{callCount}.{target}" );
			Trace.WriteLine( $"{callCount}.{target}" );
			foreach( var childTarget in target.Children )
			{
				DispTarget( childTarget, calledTargets );
			}
		}
		private static void AddNewTargets( ExecTargetInfo root, ProjectTracer tracer, ExecTargetInfo parentTarget, string name )
		{
			var target = tracer.ProjectInstance.Targets[name];
			var newTarget = new ExecTargetInfo( parentTarget, name, target.FullPath );
			var dependsOn = tracer.ResolveProperties( target.DependsOnTargets );
			foreach( var depTarget in dependsOn )
			{
				AddNewTargets( root, tracer, newTarget, depTarget );
			}
		}
		private static void InsertNonSettingTargets( ExecTargetInfo parentTarget, ProjectTracer tracer, ProjectTargetElement target )
		{
			int afterPos = -1;
			int beforePos = -1;
			for( int index = 0 ; index < parentTarget.Children.Count ; index++ )
			{
				if( parentTarget.Children[index].Name == target.AfterTargets )
				{
					afterPos = index;
					break;
				}
			}
			//	アフターの後に出るオブジェクトを探す
			for( int index = afterPos+1 ; index < parentTarget.Children.Count ; index++ )
			{
				if(parentTarget.Children[index].Name == target.BeforeTargets )
				{
					beforePos = index;
					break;
				}
			}
			//	ここではBeforeの直前に差し込むことを想定して作る(仕様上はどこに入ってもいいことになる)
			int insPos = -1;
			if( beforePos != -1 )
			{
				//	場所が確定しないので直前に入れておく
				if( afterPos == -1 )
				{
					insPos = beforePos;
				}
				else
				{
					insPos = afterPos + 1;
				}
			}
			else if( afterPos != -1 )
			{
				insPos = afterPos + 1;
			}
			if( insPos != -1 )
			{
				var insTarget = new ExecTargetInfo( parentTarget, target.Name, target.Location.File );
			}
			foreach( var childTarget in parentTarget.Children )
			{
				InsertNonSettingTargets( childTarget, tracer, target );
			}
		}
	}
}
