export default function SectionTitle({ plain, accent, bg, action }) {
  return (
    <div className="relative flex items-end justify-between border-b border-gray-300 pb-3">
      <h2 className="relative text-2xl">
        <span aria-hidden className="pointer-events-none absolute -top-7 right-0 select-none text-6xl font-light uppercase text-gray-300/70">{bg}</span>
        <span className="relative">{plain} <span className="text-brand">{accent}</span></span>
      </h2>
      {action}
    </div>
  );
}
