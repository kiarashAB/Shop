import { useEffect, useState } from 'react';
import api from '../api/client';

export default function useApi(url, params) {
  const [state, setState] = useState({ data: null, loading: true, error: null });
  const key = JSON.stringify(params ?? {});

  useEffect(() => {
    const controller = new AbortController();
    setState((s) => ({ ...s, loading: true, error: null }));
    api.get(url, { params: JSON.parse(key), signal: controller.signal })
      .then((r) => setState({ data: r.data, loading: false, error: null }))
      .catch((e) => { if (!controller.signal.aborted) setState({ data: null, loading: false, error: e }); });
    return () => controller.abort();
  }, [url, key]);

  return state;
}
