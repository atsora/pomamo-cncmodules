// Copyright (C) 2026 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Where this module gets its loggers from.
  ///
  /// A cnc module is created by its host without any dependency injection, so the logger factory
  /// can not be injected: the host sets it here once, before the first module is used.
  ///
  /// Nothing is logged until the host does so. That is deliberate: this module is under the GPL-2.0
  /// and it therefore only depends on Microsoft.Extensions.Logging.Abstractions, whose license, MIT,
  /// suits it, unlike log4net. A host that logs with log4net can still collect these logs by setting
  /// a factory that forwards to it, for example with Lemoine.Core.Extensions.Logging.LoggerProvider.
  /// </summary>
  public static class OpcUaClientLogging
  {
    static ILoggerFactory s_loggerFactory = NullLoggerFactory.Instance;

    /// <summary>
    /// Factory the loggers of this module are created from
    ///
    /// Setting it to null restores the factory that logs nothing
    /// </summary>
    public static ILoggerFactory LoggerFactory
    {
      get => s_loggerFactory;
      set => s_loggerFactory = value ?? NullLoggerFactory.Instance;
    }

    /// <summary>
    /// Create a logger for a category
    /// </summary>
    /// <param name="categoryName">not null or empty</param>
    public static ILogger CreateLogger (string categoryName) => s_loggerFactory.CreateLogger (categoryName);
  }
}
