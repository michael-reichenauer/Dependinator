using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;


namespace Dependinator.Utils.UI.Mvvm
{
	internal abstract class ViewModel : Notifyable
	{
		private readonly Dictionary<string, ICommand> commands = new Dictionary<string, ICommand>();
		private readonly Dictionary<string, BusyIndicator> busyIndicators =
			new Dictionary<string, BusyIndicator>();
	

		protected BusyIndicator BusyIndicator([CallerMemberName] string memberName = "")
		{
			BusyIndicator busyIndicator;
			if (!busyIndicators.TryGetValue(memberName, out busyIndicator))
			{

				busyIndicator = new BusyIndicator(memberName, OnPropertyChanged);
				busyIndicators[memberName] = busyIndicator;
			}

			return busyIndicator;
		}


		protected Command<T> Command<T>(
			Action<T> executeMethod, [CallerMemberName] string memberName = "")
		{
			ICommand command;
			if (!commands.TryGetValue(memberName, out command))
			{

				command = new Command<T>(executeMethod, GetType() + "." + memberName);
				commands[memberName] = command;
			}

			return (Command<T>)command;
		}

		protected Command<T> Command<T>(
			Action<T> executeMethod, Func<T, bool> canExecuteMethod, [CallerMemberName] string memberName = "")
		{
			ICommand command;
			if (!commands.TryGetValue(memberName, out command))
			{

				command = new Command<T>(executeMethod, canExecuteMethod, GetType() + "." + memberName);
				commands[memberName] = command;
			}

			return (Command<T>)command;
		}


		protected Command Command(Action executeMethod, [CallerMemberName] string memberName = "")
		{
			ICommand command;
			if (!commands.TryGetValue(memberName, out command))
			{

				command = new Command(executeMethod, GetType() + "." + memberName);
				commands[memberName] = command;
			}

			return (Command)command;
		}


		protected Command Command(
			Action executeMethod, Func<bool> canExecuteMethod, [CallerMemberName] string memberName = "")
		{
			ICommand command;
			if (!commands.TryGetValue(memberName, out command))
			{

				command = new Command(executeMethod, canExecuteMethod, GetType() + "." + memberName);
				commands[memberName] = command;
			}

			return (Command)command;
		}



		protected Command AsyncCommand(
			Func<Task> executeMethodAsync, [CallerMemberName] string memberName = "")
		{
			ICommand command;
			if (!commands.TryGetValue(memberName, out command))
			{

				command = new Command(executeMethodAsync, GetType() + "." + memberName);
				commands[memberName] = command;
			}

			return (Command)command;
		}


		protected Command AsyncCommand(
			Func<Task> executeMethodAsync, Func<bool> canExecuteMethod, [CallerMemberName] string memberName = "")
		{
			ICommand command;
			if (!commands.TryGetValue(memberName, out command))
			{

				command = new Command(executeMethodAsync, canExecuteMethod, GetType() + "." + memberName);
				commands[memberName] = command;
			}

			return (Command)command;
		}


		protected Command<T> AsyncCommand<T>(
			Func<T, Task> executeMethodAsync, [CallerMemberName] string memberName = "")
		{
			ICommand command;
			if (!commands.TryGetValue(memberName, out command))
			{

				command = new Command<T>(executeMethodAsync, GetType() + "." + memberName);
				commands[memberName] = command;
			}

			return (Command<T>)command;
		}


		protected Command<T> AsyncCommand<T>(
			Func<T, Task> executeMethodAsync, Func<T, bool> canExecuteMethod, [CallerMemberName] string memberName = "")
		{
			ICommand command;
			if (!commands.TryGetValue(memberName, out command))
			{

				command = new Command<T>(executeMethodAsync, canExecuteMethod, GetType() + "." + memberName);
				commands[memberName] = command;
			}

			return (Command<T>)command;
		}
	}
}