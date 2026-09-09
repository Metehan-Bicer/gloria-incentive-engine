import type { SVGProps } from 'react'

const base: SVGProps<SVGSVGElement> = {
  width: 18,
  height: 18,
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.8,
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
  'aria-hidden': true,
}

export const Icons = {
  wallet: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="M3 7a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
      <path d="M16 12h5" />
      <circle cx="16" cy="12" r="1" />
    </svg>
  ),
  rules: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="M4 6h16M4 12h10M4 18h7" />
      <path d="m17 15 2 2 4-4" />
    </svg>
  ),
  calendar: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M3 10h18M8 3v4M16 3v4" />
    </svg>
  ),
  upload: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="M12 16V4m0 0-4 4m4-4 4 4" />
      <path d="M4 16v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" />
    </svg>
  ),
  close: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="M6 6l12 12M18 6 6 18" />
    </svg>
  ),
  menu: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="M4 7h16M4 12h16M4 17h16" />
    </svg>
  ),
  plus: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="M12 5v14M5 12h14" />
    </svg>
  ),
  refresh: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="M20 12a8 8 0 1 1-2.34-5.66" />
      <path d="M20 4v5h-5" />
    </svg>
  ),
  lock: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <rect x="5" y="11" width="14" height="10" rx="2" />
      <path d="M8 11V8a4 4 0 0 1 8 0v3" />
    </svg>
  ),
  play: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="M7 5v14l11-7z" />
    </svg>
  ),
  user: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21a8 8 0 0 1 16 0" />
    </svg>
  ),
  chevronRight: (p: SVGProps<SVGSVGElement>) => (
    <svg {...base} {...p}>
      <path d="m9 6 6 6-6 6" />
    </svg>
  ),
}
