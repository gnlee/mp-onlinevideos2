﻿using System;
using System.Threading;
using MediaPortal.GUI.Library;
using MediaPortal.Dialogs;

namespace OnlineVideos
{
    internal class Gui2UtilConnector
    {
        # region Singleton
        protected Gui2UtilConnector() { }
        protected static Gui2UtilConnector instance = null;
        internal static Gui2UtilConnector Instance 
        {
            get 
            {
                if (instance == null) instance = new Gui2UtilConnector();
                return instance;
            }
        }
        #endregion

        protected bool isBusy = false;
        internal bool IsBusy { get { return isBusy; } }

        /// <summary>
        /// This method should be used to call methods from site utils that might take a few seconds.
        /// It makes sure only on thread at a time executes and has a timeout for the execution.
        /// It also catches Execeptions from the utils and writes errors to the log.
        /// </summary>
        /// <param name="task">a delegate pointing to the (anonymous) method to invoke.</param>
        /// <returns>true, if execution finished successfully before the timeout.</returns>
        internal bool ExecuteInBackgroundAndWait(ThreadStart task, string taskdescription)
        {
            // make sure only one background task can be executed at a time
            if (Monitor.TryEnter(this))
            {
                isBusy = true;
                OnlineVideosException error = null;
                bool? result = null; // while this is null the task has not finished (or later on timeouted), true indicates successfull completion and false error                
                try
                {
                    GUIWaitCursor.Init(); GUIWaitCursor.Show(); // init and show the wait cursor in MediaPortal
                    DateTime end = DateTime.Now.AddSeconds(OnlineVideoSettings.getInstance().utilTimeout); // point in time until we wait for the execution of this task
                    #if DEBUG
                        if (System.Diagnostics.Debugger.IsAttached) end = DateTime.Now.AddYears(1); // basically disable timeout when debugging
                    #endif
                    Thread backgroundThread = new Thread(delegate()
                        {
                            try
                            {                                
                                task.Invoke();
                                result = true;
                            }
                            catch (ThreadAbortException)
                            {
                                Log.Error("Timeout waiting for results.");
                                Thread.ResetAbort();
                            }
                            catch (Exception threadException)
                            {
                                error = threadException as OnlineVideosException;
                                Log.Error(threadException);
                                result = false;
                            }
                        }
                        ) { Name = "OnlineVideos", IsBackground = true };
                    backgroundThread.Start();

                    while (result == null)
                    {
                        GUIWindowManager.Process();
                        if (DateTime.Now > end) { backgroundThread.Abort(); break; }
                    }                                        
                }
                catch (Exception ex)
                {
                    result = false;
                    Log.Error(ex);
                }
                finally
                {
                    GUIWaitCursor.Hide(); // hide the wait cursor
                    if (result != true)   // show an error message if task was not completed successfully
                    {
                        if (error != null)
                        {
                            MediaPortal.Dialogs.GUIDialogOK dlg_error = (MediaPortal.Dialogs.GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
                            dlg_error.SetHeading(OnlineVideoSettings.PLUGIN_NAME);
                            dlg_error.SetLine(1, string.Format("{0} {1}", Translation.Error, taskdescription));
                            dlg_error.SetLine(2, error.Message);
                            dlg_error.DoModal(GUIWindowManager.ActiveWindow);
                        }
                        else
                        {
                            GUIDialogNotify dlg_error = (GUIDialogNotify)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_NOTIFY);
                            dlg_error.SetHeading(OnlineVideoSettings.PLUGIN_NAME);
                            if (result.HasValue)
                                dlg_error.SetText(string.Format("{0} {1}", Translation.Error, taskdescription));
                            else
                                dlg_error.SetText(string.Format("{0} {1}", Translation.Timeout, taskdescription));
                            dlg_error.DoModal(GUIWindowManager.ActiveWindow);
                        }
                    }
                    Monitor.Exit(this);
                    isBusy = false;
                }
                return result == true;
            }
            else
            {
                Log.Error("Another thread tried to execute a task in background.");
                return false;
            }
        }

    }
}
