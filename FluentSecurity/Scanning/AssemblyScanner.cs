﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentSecurity.Scanning.TypeScanners;

namespace FluentSecurity.Scanning
{
	public class AssemblyScanner
	{
		private readonly List<Assembly> _assemblies = new List<Assembly>();
		private readonly List<ITypeScanner> _scanners = new List<ITypeScanner>();
		private readonly IList<Func<Type, bool>> _filters = new List<Func<Type, bool>>();

		public IEnumerable<Assembly> AssembliesToScan
		{
			get { return _assemblies; }
		}

		public void Assembly(Assembly assembly)
		{
			if (assembly == null) throw new ArgumentNullException("assembly");
			_assemblies.Add(assembly);
		}

		public void Assemblies(IEnumerable<Assembly> assemblies)
		{
			if (assemblies == null) throw new ArgumentNullException("assemblies");

			var assembliesToScan = assemblies.ToList();
			if (assembliesToScan.Any(a => a == null)) throw new ArgumentException("Assemblies must not contain null values.", "assemblies");

			assembliesToScan.ForEach(Assembly);
		}

		public void TheCallingAssembly()
		{
			var callingAssembly = FindCallingAssembly();
			if (callingAssembly != null) _assemblies.Add(callingAssembly);
		}

		private static Assembly FindCallingAssembly()
		{
			var thisAssembly = System.Reflection.Assembly.GetExecutingAssembly();
			Assembly callingAssembly = null;

			var trace = new StackTrace(false);
			for (var i = 0; i < trace.FrameCount; i++)
			{
				var frame = trace.GetFrame(i);
				var assembly = frame.GetMethod().DeclaringType.Assembly;
				if (assembly != thisAssembly)
				{
					callingAssembly = assembly;
					break;
				}
			}
			return callingAssembly;
		}

		public void AssembliesFromApplicationBaseDirectory()
		{
			AssembliesFromApplicationBaseDirectory(a => true);
		}

		public void AssembliesFromApplicationBaseDirectory(Predicate<Assembly> assemblyFilter)
		{
			var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			AssembliesFromPath(baseDirectory, assemblyFilter);
		}

		private void AssembliesFromPath(string path, Predicate<Assembly> assemblyFilter)
		{
			var assemblyPaths = Directory.GetFiles(path).Where(file =>
			{
				var extension = Path.GetExtension(file);
				return extension != null && (
					extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
					extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
					);
			}).ToList();

			foreach (var assemblyPath in assemblyPaths)
			{
				var assembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
				if (assembly != null && assemblyFilter.Invoke(assembly))
					Assembly(assembly);
			}
		}

		public void With(ITypeScanner typeScanner)
		{
			_scanners.Add(typeScanner);
		}

		public void With<TTypeScanner>() where TTypeScanner : ITypeScanner, new()
		{
			With(new TTypeScanner());
		}

		public void IncludeNamespaceContainingType<T>()
		{
			Func<Type, bool> predicate = type =>
			{
				var currentNamespace = type.Namespace ?? "";
				var expectedNamespace = typeof (T).Namespace ?? "";
				return currentNamespace.StartsWith(expectedNamespace);
			};
			_filters.Add(predicate);
		}

		public IEnumerable<Type> Scan()
		{
			var results = new List<Type>();
			_scanners.Each(scanner => scanner.Scan(_assemblies).Where(type =>
				_filters.Any() == false || _filters.Any(filter => filter.Invoke(type))).Each(results.Add)
				);
			return results;
		}
	}
}