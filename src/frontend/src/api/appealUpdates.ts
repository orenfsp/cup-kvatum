import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export function createAppealUpdatesConnection(onUpdate: () => void) {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/appeals')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
  connection.on('appealUpdated', onUpdate);
  return connection;
}

export function createExpertWorkUpdatesConnection(onUpdate: () => void) {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/appeals')
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
  connection.on('expertWorkUpdated', onUpdate);
  return connection;
}
