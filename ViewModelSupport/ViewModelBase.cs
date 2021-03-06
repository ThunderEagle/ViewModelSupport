﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;

namespace ViewModelSupport
{
    public class ViewModelBase :
#if !__MOBILE__
        DynamicObject,
#endif
        INotifyPropertyChanged
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private readonly IDictionary<string, List<string>> _propertyMap;
        private readonly IDictionary<string, List<string>> _methodMap;
        private readonly IDictionary<string, List<string>> _commandMap;

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
        protected class DependsUponAttribute : Attribute
        {
            public string DependencyName { get; private set; }

            public bool VerifyStaticExistence { get; set; }

            public DependsUponAttribute(string propertyName)
            {
                DependencyName = propertyName;
            }
        }

        //private static readonly bool _isInDesignMode;

        //static ViewModelBase()
        //{
        //var prop = DesignerProperties.IsInDesignModeProperty;
        //_isInDesignMode
        //	= (bool)DependencyPropertyDescriptor
        //			.FromProperty(prop, typeof(FrameworkElement))
        //			.Metadata.DefaultValue;

        //}

        //protected bool IsInDesignMode => _isInDesignMode;

        public ViewModelBase()
        {
            _propertyMap = MapDependencies<DependsUponAttribute>(() => GetType().GetProperties());
            _methodMap = MapDependencies<DependsUponAttribute>(() => GetType().GetMethods().Cast<MemberInfo>().Where(method => !method.Name.StartsWith(CAN_EXECUTE_PREFIX)));
            _commandMap = MapDependencies<DependsUponAttribute>(() => GetType().GetMethods().Cast<MemberInfo>().Where(method => method.Name.StartsWith(CAN_EXECUTE_PREFIX)));
            CreateCommands();
            VerifyDependencies();
        }

        protected T Get<T>(string name)
        {
            return Get(name, default(T));
        }

        protected T Get<T>(string name, T defaultValue)
        {
            if (_values.ContainsKey(name))
            {
                return (T)_values[name];
            }

            return defaultValue;
        }

        protected T Get<T>(string name, Func<T> initialValue)
        {
            if (_values.ContainsKey(name))
            {
                return (T)_values[name];
            }

            Set(name, initialValue());
            return Get<T>(name);
        }

        protected T Get<T>(Expression<Func<T>> expression)
        {
            return Get<T>(PropertyName(expression));
        }

        protected T Get<T>(Expression<Func<T>> expression, T defaultValue)
        {
            return Get(PropertyName(expression), defaultValue);
        }

        protected T Get<T>(Expression<Func<T>> expression, Func<T> initialValue)
        {
            return Get(PropertyName(expression), initialValue);
        }

        public void Set<T>(string name, T value)
        {
            if (_values.ContainsKey(name))
            {
                if (_values[name] == null && value == null)
                {
                    return;
                }

                if (_values[name] != null && _values[name].Equals(value))
                {
                    return;
                }

                _values[name] = value;
            }
            else
            {
                _values.Add(name, value);
            }

            RaisePropertyChanged(name);
        }

        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged.Raise(this, name);
            if (_propertyMap.ContainsKey(name))
            {
                _propertyMap[name].Each(RaisePropertyChanged);
            }
            ExecuteDependentMethods(name);
            FireChangesOnDependentCommands(name);
        }

        private void ExecuteDependentMethods(string name)
        {
            if (_methodMap.ContainsKey(name))
            {
                _methodMap[name].Each(ExecuteMethod);
            }
        }

        private void FireChangesOnDependentCommands(string name)
        {
            if (_commandMap.ContainsKey(name))
            {
                _commandMap[name].Each(RaiseCanExecuteChangedEvent);
            }
        }

        protected void Set<T>(Expression<Func<T>> expression, T value)
        {
            Set(PropertyName(expression), value);
        }

        private static string PropertyName<T>(Expression<Func<T>> expression)
        {
            var memberExpression = expression.Body as MemberExpression;

            if (memberExpression == null)
            {
                throw new ArgumentException("expression must be a property expression");
            }

            return memberExpression.Member.Name;
        }

#if !__MOBILE__
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = Get<object>(binder.Name);

            return result != null || base.TryGetMember(binder, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            var result = base.TrySetMember(binder, value);
            if (result)
            {
                return true;
            }

            Set(binder.Name, value);
            return true;
        }
#endif

        public event PropertyChangedEventHandler PropertyChanged;

        private void CreateCommands()
        {
            CommandNames.Each(name => Set(name, new DelegateCommand<object>(x => ExecuteCommand(name, x), x => CanExecuteCommand(name, x))));
        }

        private const string EXECUTE_PREFIX = "Execute_";
        private const string CAN_EXECUTE_PREFIX = "CanExecute_";

        private IEnumerable<string> CommandNames
        {
            get
            {
                return from method in GetType().GetMethods()
                       where method.Name.StartsWith(EXECUTE_PREFIX)
                       select method.Name.StripLeft(EXECUTE_PREFIX.Length);
            }
        }

        private void ExecuteCommand(string name, object parameter)
        {
            var methodInfo = GetType().GetMethod(EXECUTE_PREFIX + name);
            if (methodInfo == null)
            {
                return;
            }

            methodInfo.Invoke(this, methodInfo.GetParameters().Length == 1 ? new[] { parameter } : null);
        }

        private bool CanExecuteCommand(string name, object parameter)
        {
            var methodInfo = GetType().GetMethod(CAN_EXECUTE_PREFIX + name);
            return methodInfo == null || (bool)methodInfo.Invoke(this, methodInfo.GetParameters().Length == 1 ? new[] { parameter } : null);
        }

        protected void RaiseCanExecuteChangedEvent(string canExecute_name)
        {
            var commandName = canExecute_name.StripLeft(CAN_EXECUTE_PREFIX.Length);
            var command = Get<DelegateCommand<object>>(commandName);
            if (command == null)
            {
                return;
            }

            command.RaiseCanExecuteChanged();
        }


        private static IDictionary<string, List<string>> MapDependencies<T>(Func<IEnumerable<MemberInfo>> getInfo) where T : DependsUponAttribute
        {
            var dependencyMap = getInfo().ToDictionary(
                                                        p => p.Name,
                                                        p => p.GetCustomAttributes(typeof(T), true)
                                                            .Cast<T>()
                                                            .Select(a => a.DependencyName)
                                                            .ToList());

            return Invert(dependencyMap);
        }

        private static IDictionary<string, List<string>> Invert(IDictionary<string, List<string>> map)
        {
            var flattened = from key in map.Keys
                            from value in map[key]
                            select new { Key = key, Value = value };

            var uniqueValues = flattened.Select(x => x.Value).Distinct();

            return uniqueValues.ToDictionary(
                                            x => x,
                                            x => (from item in flattened
                                                  where item.Value == x
                                                  select item.Key).ToList());
        }

        private void ExecuteMethod(string name)
        {
            var memberInfo = GetType().GetMethod(name);
            if (memberInfo == null)
            {
                return;
            }

            memberInfo.Invoke(this, null);
        }

        private void VerifyDependencies()
        {
            var methods = GetType().GetMethods().Cast<MemberInfo>();
            var properties = GetType().GetProperties();

            var propertyNames = methods.Union(properties)
                                        .SelectMany(method => method.GetCustomAttributes(typeof(DependsUponAttribute), true).Cast<DependsUponAttribute>())
                                        .Where(attribute => attribute.VerifyStaticExistence)
                                        .Select(attribute => attribute.DependencyName);

            propertyNames.Each(VerifyDependency);
        }

        private void VerifyDependency(string propertyName)
        {
            var property = GetType().GetProperty(propertyName);
            if (property == null)
            {
                throw new ArgumentException("DependsUpon Property Does Not Exist: " + propertyName);
            }
        }
    }
}