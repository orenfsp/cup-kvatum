import type { DetailedHTMLProps, HTMLAttributes } from 'react';

type MaterialButtonProps = DetailedHTMLProps<HTMLAttributes<HTMLElement>, HTMLElement> & {
  disabled?: boolean;
  type?: 'button' | 'submit' | 'reset';
};

declare module 'react' {
  namespace JSX {
    interface IntrinsicElements {
      'md-filled-button': MaterialButtonProps;
      'md-outlined-button': MaterialButtonProps;
    }
  }
}

export {};

