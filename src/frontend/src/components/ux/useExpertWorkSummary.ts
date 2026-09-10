import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { createExpertWorkUpdatesConnection } from '../../api/appealUpdates';
import { getExpertWorkSummary } from '../../api/expertAppeals';

export function useExpertWorkSummary(enabled: boolean) {
  const queryClient = useQueryClient();
  const summaryQuery = useQuery({
    queryKey: ['expert-work-summary'],
    queryFn: getExpertWorkSummary,
    enabled,
    refetchInterval: 30_000,
  });

  useEffect(() => {
    if (!enabled) return undefined;
    const refresh = () => {
      void queryClient.invalidateQueries({ queryKey: ['expert-work-summary'] });
      void queryClient.invalidateQueries({ queryKey: ['expert-appeals'] });
    };
    const connection = createExpertWorkUpdatesConnection(refresh);
    const join = () => connection.invoke('JoinExpertWork').catch(() => undefined);
    connection.onreconnected(join);
    void connection.start().then(join).catch(() => undefined);
    return () => { void connection.stop(); };
  }, [enabled, queryClient]);

  return summaryQuery;
}
