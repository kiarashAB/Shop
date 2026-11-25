import { ORDER_STATUS } from '../lib/labels';

export default function StatusBadge({ status }) {
  const s = ORDER_STATUS[status] ?? { label: status, cls: 'bg-gray-100 text-gray-600' };
  return <span className={`rounded-full px-3 py-1 text-xs ${s.cls}`}>{s.label}</span>;
}
